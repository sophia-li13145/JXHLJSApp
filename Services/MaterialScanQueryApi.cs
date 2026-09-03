using System.Net.Http.Json;
using System.Text.Json;
using JXHLJSApp.Models;
using JXHLJSApp.Services.Common;

namespace JXHLJSApp.Services;

public interface IMaterialScanQueryApi
{
    Task<MaterialScanQueryDto> QueryAsync(string qrCode, CancellationToken ct = default);
}

public sealed class MaterialScanQueryApi : IMaterialScanQueryApi
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly string _endpoint;

    public MaterialScanQueryApi(HttpClient http, IConfigLoader configLoader)
    {
        _http = http;
        var servicePath = http.BaseAddress?.AbsolutePath?.TrimEnd('/') ?? "/jxhljszpService";
        _endpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("materialScanQuery.completeInfo", "/pda/wmsMaterialQrCode/scanQueryCompleteMaterialInfo"),
            servicePath);
    }

    public async Task<MaterialScanQueryDto> QueryAsync(string qrCode, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _endpoint);
        using var response = await _http.PostAsJsonAsync(url, new { qrCode }, JsonOptions, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<MaterialScanQueryDto>>(stream, JsonOptions, ct).ConfigureAwait(false);
        if (data is null || !data.success || data.result is null)
        {
            throw new InvalidOperationException(data?.message ?? "未查询到物料信息");
        }

        return data.result;
    }
}
