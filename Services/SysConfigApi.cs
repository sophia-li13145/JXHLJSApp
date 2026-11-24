using System.Text.Json;
using JXHLJSApp.Models;
using JXHLJSApp.Services.Common;

namespace JXHLJSApp.Services;

public interface ISysConfigApi
{
    Task<SysConfigDto?> GetConfigByCodeAsync(string configCode, CancellationToken ct = default);
}

public sealed class SysConfigApi : ISysConfigApi
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly string _endpoint;

    public SysConfigApi(HttpClient http, IConfigLoader configLoader)
    {
        _http = http;
        var servicePath = http.BaseAddress?.AbsolutePath?.TrimEnd('/') ?? "/jxhljszpService";
        _endpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("sysConfig.getConfigByCode", "/pda/sysConfig/getConfigByCode"),
            servicePath);
    }

    public async Task<SysConfigDto?> GetConfigByCodeAsync(string configCode, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configCode);
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _endpoint);
        var separator = url.Contains('?') ? "&" : "?";
        using var response = await _http.GetAsync(
            $"{url}{separator}configCode={Uri.EscapeDataString(configCode)}", ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<SysConfigDto>>(stream, JsonOptions, ct).ConfigureAwait(false);
        if (data is null || !data.success)
            throw new InvalidOperationException(data?.message ?? "查询系统配置失败");

        // 查询成功但配置不存在时，后端合法返回 result: null。
        return data.result;
    }
}
