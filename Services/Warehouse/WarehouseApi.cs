using JXHLJSApp.Models;
using JXHLJSApp.Models.Warehouse;
using JXHLJSApp.Services.Common;
using System.Net.Http.Json;
using System.Text.Json;

namespace JXHLJSApp.Services.Warehouse;

public interface IWarehouseApi
{
    Task<List<RawMaterialReceivingDto>> GetRawMaterialReceivingListAsync(CancellationToken ct = default);
    Task<BlankInstockDto> AddBlankInstockAsync(CancellationToken ct = default);
    Task<List<WarehouseInfoDto>> QueryWarehouseInfoAsync(CancellationToken ct = default);
    Task<AttachmentDto> UploadAttachmentAsync(FileResult photo, string attachmentFolder, string attachmentLocation, CancellationToken ct = default);
    Task<RawMaterialOcrDto> RecognizeIncomingAsync(AttachmentDto fileInfo, string instockNo, CancellationToken ct = default);
}

public sealed class WarehouseApi : IWarehouseApi
{
    private readonly HttpClient _http;
    private readonly string _rawMaterialReceivingListEndpoint;
    private readonly string _addBlankInstockEndpoint;
    private readonly string _queryWarehouseInfoEndpoint;
    private readonly string _uploadAttachmentEndpoint;
    private readonly string _ocrIncomingEndpoint;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public WarehouseApi(HttpClient http, IConfigLoader configLoader)
    {
        _http = http;
        var servicePath = _http.BaseAddress?.AbsolutePath?.TrimEnd('/') ?? "/jxhljszpService";
        _rawMaterialReceivingListEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("rawMaterialReceiving.listInStock", "/pda/rawMaterialReceiving/listInStock"), servicePath);
        _addBlankInstockEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("rawMaterialReceiving.addBlankInstock", "/pda/rawMaterialReceiving/addBlankInstock"), servicePath);
        _queryWarehouseInfoEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("rawMaterialReceiving.queryWarehouseInfo", "/pda/rawMaterialReceiving/queryWarehouseInfo"), servicePath);
        _uploadAttachmentEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("attachment.uploadAttachment", "/pda/attachment/uploadAttachment"), servicePath);
        _ocrIncomingEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("rawMaterialReceiving.ocrIncoming", "/pda/rawMaterialReceiving/ocrIncoming"), servicePath);
    }

    public async Task<List<RawMaterialReceivingDto>> GetRawMaterialReceivingListAsync(CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _rawMaterialReceivingListEndpoint);
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<List<RawMaterialReceivingDto>>(resp, ct).ConfigureAwait(false);
        return data.result ?? new List<RawMaterialReceivingDto>();
    }

    public async Task<BlankInstockDto> AddBlankInstockAsync(CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _addBlankInstockEndpoint);
        using var resp = await _http.PostAsync(url, new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()), ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<BlankInstockDto>(resp, ct).ConfigureAwait(false);
        return data.result ?? new BlankInstockDto();
    }

    public async Task<List<WarehouseInfoDto>> QueryWarehouseInfoAsync(CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _queryWarehouseInfoEndpoint);
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<List<WarehouseInfoDto>>(resp, ct).ConfigureAwait(false);
        return data.result ?? new List<WarehouseInfoDto>();
    }

    public async Task<AttachmentDto> UploadAttachmentAsync(FileResult photo, string attachmentFolder, string attachmentLocation, CancellationToken ct = default)
    {
        var query = new Dictionary<string, string?>
        {
            [nameof(attachmentFolder)] = attachmentFolder,
            [nameof(attachmentLocation)] = attachmentLocation
        };
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, BuildUrlWithQuery(_uploadAttachmentEndpoint, query));
        await using var stream = await photo.OpenReadAsync().ConfigureAwait(false);
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(photo.ContentType ?? "image/jpeg");
        content.Add(fileContent, "file", photo.FileName);
        using var resp = await _http.PostAsync(url, content, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<AttachmentDto>(resp, ct).ConfigureAwait(false);
        return data.result ?? new AttachmentDto
        {
            attachmentFolder = attachmentFolder,
            attachmentLocation = attachmentLocation,
            attachmentName = photo.FileName,
            attachmentRealName = photo.FileName
        };
    }

    public async Task<RawMaterialOcrDto> RecognizeIncomingAsync(AttachmentDto fileInfo, string instockNo, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _ocrIncomingEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, new { fileInfo, instockNo }, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<RawMaterialOcrDto>(resp, ct).ConfigureAwait(false);
        return data.result ?? new RawMaterialOcrDto();
    }

    private static async Task<ApiResp<T>> ReadApiResponseAsync<T>(HttpResponseMessage resp, CancellationToken ct)
    {
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<T>>(stream, JsonOptions, ct).ConfigureAwait(false);
        if (data is null)
        {
            throw new InvalidOperationException("接口返回为空。");
        }
        if ((data.code.HasValue && data.code.Value != 0) || (data.success == false && data.result is null))
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(data.message) ? "接口返回失败。" : data.message);
        }
        return data;
    }

    private static string BuildUrlWithQuery(string endpoint, IReadOnlyDictionary<string, string?> query)
    {
        var pairs = query
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}")
            .ToArray();
        return pairs.Length == 0 ? endpoint : $"{endpoint}?{string.Join("&", pairs)}";
    }
}
