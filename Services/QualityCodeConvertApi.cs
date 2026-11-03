using System.Net.Http.Json;
using System.Text.Json;
using JXHLJSApp.Models;
using JXHLJSApp.Models.Quality;
using JXHLJSApp.Services.Common;

namespace JXHLJSApp.Services;

public interface IQualityCodeConvertApi
{
    Task<QualityCodeMaterialDto> ScanMaterialAsync(string qrCode, CancellationToken ct = default);
    Task<QualityCodeConvertResultDto> TransCodeAsync(string sourceQrCode, string targetQrCode, CancellationToken ct = default);
    Task CancelByIdAsync(string id, CancellationToken ct = default);
    Task CancelByTargetQrCodeAsync(string targetQrCode, CancellationToken ct = default);
    Task<QualityCodeRecordPageDto> GetRecordsAsync(int pageNo, int pageSize, CancellationToken ct = default);
}

public sealed class QualityCodeConvertApi : IQualityCodeConvertApi
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly string _scanMaterialEndpoint;
    private readonly string _transCodeEndpoint;
    private readonly string _cancelTransCodeEndpoint;
    private readonly string _recordsEndpoint;

    public QualityCodeConvertApi(HttpClient http, IConfigLoader configLoader)
    {
        _http = http;
        var servicePath = http.BaseAddress?.AbsolutePath?.TrimEnd('/') ?? "/jxhljszpService";
        _scanMaterialEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("qualityCodeConvert.scanMaterial", "/pda/wmsMaterialQrCode/scanQueryMaterialInfo"), servicePath);
        _transCodeEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("qualityCodeConvert.transCode", "/pda/qualityTransCode/transCode"), servicePath);
        _cancelTransCodeEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("qualityCodeConvert.cancelTransCode", "/pda/qualityTransCode/cancelTransCode"), servicePath);
        _recordsEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("qualityCodeConvert.pageRecord", "/pda/qualityTransCode/pageRecord"), servicePath);
    }

    public async Task<QualityCodeMaterialDto> ScanMaterialAsync(string qrCode, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _scanMaterialEndpoint);
        using var response = await _http.PostAsJsonAsync(url, new { qrCode }, JsonOptions, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<QualityCodeMaterialDto>>(stream, JsonOptions, ct).ConfigureAwait(false);
        if (data is null || !data.success || data.result is null)
            throw new InvalidOperationException(data?.message ?? "未查询到原物料信息，请重新扫码。");

        // A successful envelope with an empty object must not advance the scan flow.
        if (string.IsNullOrWhiteSpace(data.result.qrCode) &&
            string.IsNullOrWhiteSpace(data.result.materialCode) && string.IsNullOrWhiteSpace(data.result.materialName))
            throw new InvalidOperationException("未查询到原物料信息，请重新扫码。");

        data.result.qrCode = string.IsNullOrWhiteSpace(data.result.qrCode) ? qrCode : data.result.qrCode;
        return data.result;
    }

    public async Task<QualityCodeConvertResultDto> TransCodeAsync(string sourceQrCode, string targetQrCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceQrCode) || string.IsNullOrWhiteSpace(targetQrCode))
            throw new ArgumentException("请先扫描原物料码和空白质检码。");

        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _transCodeEndpoint);
        using var response = await _http.PostAsJsonAsync(url, new { sourceQrCode, targetQrCode }, JsonOptions, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<QualityCodeConvertResultDto>>(stream, JsonOptions, ct).ConfigureAwait(false);
        if (data is null || !data.success)
            throw new InvalidOperationException(data?.message ?? "转码失败，请重试。");

        // The mutation has succeeded even if optional result fields are omitted.
        var result = data.result ?? new QualityCodeConvertResultDto();
        result.qrCode = string.IsNullOrWhiteSpace(result.qrCode) ? targetQrCode : result.qrCode;
        return result;
    }

    public async Task<QualityCodeRecordPageDto> GetRecordsAsync(int pageNo, int pageSize, CancellationToken ct = default)
    {
        if (pageNo < 1 || pageSize < 1) throw new ArgumentOutOfRangeException(nameof(pageNo));
        var separator = _recordsEndpoint.Contains('?') ? "&" : "?";
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress,
            $"{_recordsEndpoint}{separator}pageNo={pageNo}&pageSize={pageSize}&searchCount=true");
        using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<QualityCodeRecordPageDto>>(stream, JsonOptions, ct).ConfigureAwait(false);
        if (data is null || !data.success || data.result is null)
            throw new InvalidOperationException(data?.message ?? "转码记录加载失败，请重试。");
        return data.result;
    }

    public Task CancelByIdAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("缺少需要取消的转码记录 ID。", nameof(id));
        return CancelTransCodeAsync(new { id }, ct);
    }

    public Task CancelByTargetQrCodeAsync(string targetQrCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(targetQrCode))
            throw new ArgumentException("缺少需要取消的质检码。", nameof(targetQrCode));
        return CancelTransCodeAsync(new { targetQrCode }, ct);
    }

    private async Task CancelTransCodeAsync(object request, CancellationToken ct)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _cancelTransCodeEndpoint);
        using var response = await _http.PostAsJsonAsync(url, request, JsonOptions, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<bool?>>(stream, JsonOptions, ct).ConfigureAwait(false);
        if (data is null || !data.success || data.result != true)
            throw new InvalidOperationException(data?.message ?? "取消转码失败，请重试。");
    }
}
