using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

public interface IConfigLoader
{
    Task EnsureLatestAsync();
    JsonNode Load();
    void Save(JsonNode node);

    // === 新增 ===
    /// <summary>根据用户名中的 @service 写入 services.current，并在缺失时给该服务名补默认路径</summary>
    void SetCurrentServiceByUser(string? userName, string defaultServiceName = "normalService", string defaultServicePath = "/normalService");

    /// <summary>获取基地址：scheme://ipAddress + services[current]</summary>
    string GetBaseUrl();

    /// <summary>获取 API 相对路径（点路径），如 "login" 或 "workOrder.page"</summary>
    string GetApiPath(string dottedPath, string fallback = "/pda/auth/login");
}

public class ConfigLoader : IConfigLoader
{
    public ConfigLoader()
    {
        ConfigLoaderStatic.EnsureConfigExists();
    }

    public Task EnsureLatestAsync() => ConfigLoaderStatic.EnsureConfigIsLatestAsync();
    public JsonNode Load() => ConfigLoaderStatic.Load();
    public void Save(JsonNode node) => ConfigLoaderStatic.Save(node);

    // === 新增 ===
    public void SetCurrentServiceByUser(string? userName, string defaultServiceName = "normalService", string defaultServicePath = "/normalService")
        => ConfigLoaderStatic.SetCurrentServiceByUser(userName, defaultServiceName, defaultServicePath);

    public string GetBaseUrl() => ConfigLoaderStatic.GetBaseUrl();

    public string GetApiPath(string dottedPath, string fallback = "/pda/auth/login")
        => ConfigLoaderStatic.GetApiPath(dottedPath, fallback);
}

public static class ConfigLoaderStatic
{
    private const string FileName = "appconfig.json";
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// 包内 schemaVersion 更高时，用包内配置刷新配置结构与接口表，
    /// 但保留管理员在现场配置过的服务器地址、服务名与服务路径、打印配置。
    /// 首次运行：直接复制包内文件到 AppData。
    /// </summary>
    public static async Task EnsureConfigIsLatestAsync()
    {
        var appDataPath = Path.Combine(FileSystem.AppDataDirectory, FileName);

        // 读包内
        JsonNode pkgNode;
        using (var s = await FileSystem.OpenAppPackageFileAsync(FileName).ConfigureAwait(false))
        using (var reader = new StreamReader(s))
        {
            pkgNode = JsonNode.Parse(await reader.ReadToEndAsync().ConfigureAwait(false))!;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(appDataPath)!);

        // 本地不存在 → 直接落地
        if (!File.Exists(appDataPath))
        {
            await File.WriteAllTextAsync(appDataPath, pkgNode.ToJsonString(JsonOpts)).ConfigureAwait(false);
            return;
        }

        // 读本地
        JsonNode localNode;
        using (var fs = new FileStream(appDataPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            localNode = JsonNode.Parse(fs)!;
        }

        var pkgVer = ReadSchemaVersion(pkgNode);
        var localVer = ReadSchemaVersion(localNode);

        // 无论版本高低，先把现场可编辑配置写进包内副本，避免升级后把现场地址冲成包内默认值。
        PreserveLocalOverrides(pkgNode, localNode);

        // 包内版本更高 → 用包内配置刷新结构与接口；IP 已在上面强制保留。
        if (CompareSchemaVersion(localVer, pkgVer) < 0)
        {
            await File.WriteAllTextAsync(appDataPath, pkgNode.ToJsonString(JsonOpts)).ConfigureAwait(false);
        }
        // 否则保持本地不动
    }

    /// <summary>读取 schemaVersion，兼容数字（17）与字符串（"V1.4_202610.29"）两种写法。</summary>
    private static string ReadSchemaVersion(JsonNode? node)
    {
        var value = node?["schemaVersion"];
        if (value is null)
        {
            return "0";
        }

        return value.GetValueKind() switch
        {
            JsonValueKind.Number => value.ToJsonString(),
            JsonValueKind.String => value.GetValue<string>()?.Trim() is { Length: > 0 } text ? text : "0",
            _ => "0"
        };
    }

    /// <summary>
    /// 把管理员在设置页可改的现场配置合并进包内配置副本：
    /// 服务器 IP、打印服务地址与打印机名、服务名与服务路径。
    /// 其余结构（apiEndpoints、rightKeyScanner、logging）以包内为准，实现配置升级。
    /// </summary>
    private static void PreserveLocalOverrides(JsonNode packageNode, JsonNode localNode)
    {
        // 1) 现场服务器 IP（含端口）
        var localIpAddress = localNode?["server"]?["ipAddress"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(localIpAddress))
        {
            var packageServer = packageNode["server"] as JsonObject;
            if (packageServer is null)
            {
                packageServer = new JsonObject();
                packageNode["server"] = packageServer;
            }

            packageServer["ipAddress"] = localIpAddress;
        }

        // 2) 现场打印配置
        if (localNode["printing"] is JsonObject localPrinting)
        {
            packageNode["printing"] = localPrinting.DeepClone();
        }

        // 3) 现场服务名与服务路径：以包内为底，用本地同名键覆盖，保证 current 不丢
        if (localNode["services"] is JsonObject localServices
            && packageNode["services"] is JsonObject packageServices)
        {
            foreach (var item in localServices.ToList())
            {
                packageServices[item.Key] = item.Value?.DeepClone();
            }
        }
    }

    /// <summary>按非字母数字分段逐段比较（数字段按数值比较），支持 V1.10 大于 V1.9。</summary>
    private static int CompareSchemaVersion(string? current, string? package)
    {
        var currentParts = SplitVersionParts(current);
        var packageParts = SplitVersionParts(package);
        var max = Math.Max(currentParts.Count, packageParts.Count);

        for (var i = 0; i < max; i++)
        {
            var left = i < currentParts.Count ? currentParts[i] : "0";
            var right = i < packageParts.Count ? packageParts[i] : "0";

            var leftIsNum = int.TryParse(left, out var leftNum);
            var rightIsNum = int.TryParse(right, out var rightNum);

            var cmp = leftIsNum && rightIsNum
                ? leftNum.CompareTo(rightNum)
                : string.Compare(left, right, StringComparison.OrdinalIgnoreCase);

            if (cmp != 0)
            {
                return cmp;
            }
        }

        return 0;
    }

    private static List<string> SplitVersionParts(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return ["0"];
        }

        var parts = new List<string>();
        var span = version.AsSpan();
        var start = 0;

        for (var i = 0; i < span.Length; i++)
        {
            if (char.IsLetterOrDigit(span[i]))
            {
                continue;
            }

            if (i > start)
            {
                parts.Add(version[start..i]);
            }

            start = i + 1;
        }

        if (start < span.Length)
        {
            parts.Add(version[start..]);
        }

        return parts.Count == 0 ? ["0"] : parts;
    }

    public static void EnsureConfigExists()
    {
        var appDataPath = Path.Combine(FileSystem.AppDataDirectory, FileName);
        if (!File.Exists(appDataPath))
        {
            EnsureConfigIsLatestAsync().GetAwaiter().GetResult();
        }
    }

    /// <summary>读取生效配置（AppData）</summary>
    public static JsonNode Load()
    {
        EnsureConfigExists();
        return JsonNode.Parse(File.ReadAllText(Path.Combine(FileSystem.AppDataDirectory, FileName)))!;
    }

    /// <summary>保存（如果你在设置页手动修改本地配置）</summary>
    public static void Save(JsonNode node)
    {
        var appDataPath = Path.Combine(FileSystem.AppDataDirectory, FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(appDataPath)!);
        File.WriteAllText(appDataPath, node.ToJsonString(JsonOpts));
    }

    // ==================== 新增能力 ====================

    /// <summary>从用户名里提取 @service；不合法或不存在则返回默认</summary>
    private static string ExtractServiceName(string? userName, string @default = "normalService")
    {
        if (string.IsNullOrWhiteSpace(userName)) return @default;
        var i = userName.LastIndexOf('@');
        if (i < 0 || i == userName.Length - 1) return @default;

        var raw = userName[(i + 1)..].Trim().ToLowerInvariant();
        // 允许 a-z 0-9 _ - .
        return Regex.IsMatch(raw, @"^[a-z0-9_\-\.]+$") ? raw : @default;
    }

    /// <summary>
    /// 根据用户名设置 services.current；
    /// 若该服务在 services 下不存在，则补一个默认路径（默认 "/normalService"），并保存。
    /// </summary>
    public static void SetCurrentServiceByUser(string? userName, string defaultServiceName = "normalService", string defaultServicePath = "/normalService")
    {
        var node = Load() ?? new JsonObject();

        var services = node["services"] as JsonObject;
        if (services is null)
        {
            services = new JsonObject();
            node["services"] = services;
        }

        var svcName = ExtractServiceName(userName, defaultServiceName);
        if (!svcName.EndsWith("service", StringComparison.OrdinalIgnoreCase))
        {
            svcName += "Service";
        }

        var inferredPath = svcName == defaultServiceName
            ? NormalizePath(defaultServicePath, "/normalService")
            : NormalizePath($"/{svcName}", $"/{svcName}");

        // 如果该服务名没有路径，则补默认路径
        if (services[svcName] is null)
        {
            services[svcName] = inferredPath;
        }

        // 记录当前生效服务名
        services["current"] = svcName;

        // 持久化
        Save(node);
    }

    /// <summary>获取基地址：scheme://ipAddress + services[current]（ipAddress 已可含端口）</summary>
    public static string GetBaseUrl()
    {
        var cfg = Load();
        var scheme = cfg?["server"]?["scheme"]?.GetValue<string>() ?? "http";
        var host = cfg?["server"]?["ipAddress"]?.GetValue<string>() ?? "127.0.0.1";
        var services = cfg?["services"] as JsonObject;

        var current = services?["current"]?.GetValue<string>() ?? "normalService";
        var servicePath = services?[current]?.GetValue<string>()
                          ?? services?["normalService"]?.GetValue<string>()
                          ?? "/normalService";
        servicePath = NormalizePath(servicePath, "/normalService");

        return $"{scheme}://{host}{servicePath}";
    }

    /// <summary>
    /// 从 apiEndpoints 里按“点路径”取相对路径（支持一层或二层），如 "login"、"workOrder.page"
    /// </summary>
    public static string GetApiPath(string dottedPath, string fallback = "/pda/auth/login")
    {
        var cfg = Load();
        var api = cfg?["apiEndpoints"];
        if (api is null) return NormalizePath(fallback, fallback);

        string? val = null;
        var parts = dottedPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 1)
        {
            val = api[parts[0]]?.GetValue<string>();
        }
        else if (parts.Length == 2)
        {
            val = api[parts[0]]?[parts[1]]?.GetValue<string>();
        }

        val ??= fallback;
        return NormalizePath(val, fallback);
    }

    private static string NormalizePath(string p, string fallback)
    {
        if (string.IsNullOrWhiteSpace(p)) return fallback;
        return p.StartsWith("/") ? p : "/" + p;
    }
}
