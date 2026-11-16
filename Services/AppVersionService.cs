using JXHLJSApp.Models;
using JXHLJSApp.Services.Common;
using JXHLJSApp.Tools;
using Serilog;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace JXHLJSApp.Services;

public interface IAppVersionService
{
    /// <summary>启动时检查版本，内部吞掉异常，不阻断启动。</summary>
    Task HandleStartupUpdateAsync(CancellationToken ct = default);

    /// <summary>登录门禁：返回 false 表示当前版本不允许登录。</summary>
    Task<bool> EnsureVersionAllowedAsync(CancellationToken ct = default);

    /// <summary>管理员页手动检查更新，与启动检查同一套逻辑。</summary>
    Task CheckManuallyAsync(CancellationToken ct = default);
}

/// <summary>
/// 服务端版本校验：上报本机应用版本与配置版本，按服务端结果提示更新或拦截登录。
/// </summary>
public sealed class AppVersionService : IAppVersionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    private readonly HttpClient _http;
    private readonly IConfigLoader _configLoader;
    private readonly string _checkUpdatePath;
    private readonly string _previewPath;

    public AppVersionService(HttpClient http, IConfigLoader configLoader)
    {
        _http = http;
        _configLoader = configLoader;

        if (_http.Timeout == Timeout.InfiniteTimeSpan)
        {
            _http.Timeout = TimeSpan.FromSeconds(15);
        }

        var servicePath = _http.BaseAddress?.AbsolutePath?.TrimEnd('/') ?? "/jxhljszpService";
        _checkUpdatePath = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("appVersion.checkUpdate", "/pda/pdaAppVersion/checkUpdate"), servicePath);
        _previewPath = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("attachment.previewAttachment", "/pda/attachment/previewAttachment"), servicePath);
    }

    public async Task HandleStartupUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            var data = await CheckUpdateAsync(ct).ConfigureAwait(false);
            if (data is not { needUpdate: true })
            {
                return;
            }

            var message = BuildUpdateMessage(data);
            var confirm = await ConfirmAsync("版本更新", message, "立即更新", "稍后").ConfigureAwait(false);
            if (!confirm)
            {
                return;
            }

            await OpenDownloadAsync(data, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // 版本检查失败绝不能阻断启动。
            Log.Warning(ex, "启动版本检查失败");
        }
    }

    public async Task<bool> EnsureVersionAllowedAsync(CancellationToken ct = default)
    {
        try
        {
            var data = await CheckUpdateAsync(ct).ConfigureAwait(false);
            if (data is null)
            {
                return true;
            }

            if (string.Equals(data.compareResult, "equal", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var isHigher = string.Equals(data.compareResult, "higher", StringComparison.OrdinalIgnoreCase);
            var message = string.IsNullOrWhiteSpace(data.message)
                ? (isHigher
                    ? $"当前版本 V{CurrentVersion} 高于企业可用版本 {data.enterpriseVersionName}，请切换到企业可用版本后再登录。"
                    : $"当前版本 V{CurrentVersion} 不在可用版本范围内，请联系管理员。")
                : data.message;

            await AlertAsync("版本提示", message).ConfigureAwait(false);
            return !isHigher;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "登录前版本校验失败，按放行处理");
            return true;
        }
    }

    public Task CheckManuallyAsync(CancellationToken ct = default) => HandleStartupUpdateAsync(ct);

    private static string CurrentVersion => AppInfo.Current.VersionString;

    private async Task<AppVersionCheckResult?> CheckUpdateAsync(CancellationToken ct)
    {
        await _configLoader.EnsureLatestAsync().ConfigureAwait(false);

        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _checkUpdatePath);

        // versionName 使用真实 APK 版本，configVersion 单独上报配置结构版本，避免两者语义混用。
        var payload = new
        {
            versionName = CurrentVersion,
            configVersion = GetConfigSchemaVersion()
        };

        using var resp = await _http.PostAsJsonAsync(url, payload, ct).ConfigureAwait(false);
        var raw = await ResponseGuard.ReadAsStringSafeAsync(resp.Content, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            Log.Warning("版本检查接口返回 {Status}: {Body}", (int)resp.StatusCode, raw);
            return null;
        }

        var result = JsonSerializer.Deserialize<ApiResp<AppVersionCheckResult>>(raw, JsonOptions);
        return result?.result;
    }

    private string GetConfigSchemaVersion()
    {
        try
        {
            var value = _configLoader.Load()?["schemaVersion"];
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
        catch
        {
            return "0";
        }
    }

    private static string BuildUpdateMessage(AppVersionCheckResult data)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.IsNullOrWhiteSpace(data.message) ? "检测到新版本，请及时更新。" : data.message);

        if (!string.IsNullOrWhiteSpace(data.enterpriseVersionName))
        {
            sb.AppendLine();
            sb.AppendLine($"企业可用版本：{data.enterpriseVersionName}");
        }

        if (!string.IsNullOrWhiteSpace(data.updateNote))
        {
            sb.AppendLine();
            sb.AppendLine("更新内容：");
            sb.AppendLine(data.updateNote);
        }

        return sb.ToString().TrimEnd();
    }

    private async Task OpenDownloadAsync(AppVersionCheckResult data, CancellationToken ct)
    {
        // 1) 优先使用后端直接返回的可匿名下载地址：未登录也能打开浏览器下载。
        var anonymousUrl = ResolveDownloadUrl(data.downloadUrl ?? data.fileInfo?.downloadUrl, _http.BaseAddress);
        if (!string.IsNullOrWhiteSpace(anonymousUrl))
        {
            await OpenInBrowserAsync(anonymousUrl).ConfigureAwait(false);
            return;
        }

        // 2) 兼容旧后端：attachmentUrl 本身就是可直连的绝对地址。
        var attachmentUrl = data.fileInfo?.attachmentUrl;
        if (Uri.TryCreate(attachmentUrl, UriKind.Absolute, out var attachmentUri))
        {
            await OpenInBrowserAsync(attachmentUri.AbsoluteUri).ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(attachmentUrl))
        {
            await AlertAsync("下载失败", "更新文件地址为空，请联系管理员。").ConfigureAwait(false);
            return;
        }

        // 3) 回退：走预览接口换预签名地址（该接口需要登录态）。
        //    先判断登录态，避免未登录时发出请求被判定为“登录失效”。
        var token = await TokenStorage.LoadAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            await AlertAsync("提示", "当前未登录，暂时无法获取安装包地址。请先登录后再更新，或联系管理员获取安装包。").ConfigureAwait(false);
            return;
        }

        try
        {
            // 换取带时效的预签名地址（10 分钟），再交给系统浏览器/下载器。
            var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, BuildUrlWithQuery(_previewPath, new Dictionary<string, string?>
            {
                ["attachmentUrl"] = attachmentUrl,
                ["expires"] = "600"
            }));

            using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            var raw = await ResponseGuard.ReadAsStringSafeAsync(resp.Content, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var preview = JsonSerializer.Deserialize<ApiResp<string>>(raw, JsonOptions);
            var downloadUrl = ResolveDownloadUrl(preview?.result, _http.BaseAddress);
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                throw new InvalidOperationException("下载地址为空。");
            }

            await OpenInBrowserAsync(downloadUrl).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取更新下载地址失败，attachmentUrl={AttachmentUrl}", attachmentUrl);
            // 不把后端原文（如 token 过期）直接弹给现场用户。
            await AlertAsync("下载失败", "无法获取安装包下载地址，请稍后重试或联系管理员。").ConfigureAwait(false);
        }
    }

    private static Task OpenInBrowserAsync(string url) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Launcher.Default.OpenAsync(new Uri(url, UriKind.Absolute));
        });

    /// <summary>
    /// 解析下载地址：绝对地址直接返回；
    /// 以 / 开头的相对地址按服务器根路径拼接，其余按当前服务路径拼接。
    /// </summary>
    private static string? ResolveDownloadUrl(string? raw, Uri? baseAddress)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
        {
            return absolute.AbsoluteUri;
        }

        if (baseAddress is null)
        {
            return null;
        }

        var rootUri = new Uri(baseAddress.GetLeftPart(UriPartial.Authority), UriKind.Absolute);
        var serviceBaseUri = new Uri(baseAddress.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
        var resolvedBase = value.StartsWith('/') ? rootUri : serviceBaseUri;

        return new Uri(resolvedBase, value).AbsoluteUri;
    }

    private static string BuildUrlWithQuery(string path, IReadOnlyDictionary<string, string?> query)
    {
        var pairs = query
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}");

        var qs = string.Join("&", pairs);
        return string.IsNullOrEmpty(qs) ? path : $"{path}{(path.Contains('?') ? '&' : '?')}{qs}";
    }

    private static Task AlertAsync(string title, string message) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = Application.Current?.MainPage;
            if (page is null)
            {
                return;
            }

            await page.DisplayAlert(title, message, "确定");
        });

    private static Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = Application.Current?.MainPage;
            if (page is null)
            {
                return false;
            }

            return await page.DisplayAlert(title, message, accept, cancel);
        });
}
