using JXHLJSApp.Models;
using JXHLJSApp.Models.Warehouse;
using JXHLJSApp.Services.Common;
using System.Net.Http.Json;
using System.Text.Json;

namespace JXHLJSApp.Services.Warehouse;

public interface IWarehouseApi
{
    Task<List<RawMaterialReceivingDto>> GetRawMaterialReceivingListAsync(CancellationToken ct = default);
    Task<List<DeliveryOrderDto>> GetNeedDeliveryOrdersAsync(CancellationToken ct = default);
    Task<DeliveryOrderDetailDto> GetDeliveryOrderDetailAsync(string deliveryNo, CancellationToken ct = default);
    Task<List<PackagingSubTaskDto>> GetPackagingSubTaskListAsync(CancellationToken ct = default);
    Task<PackagingSubTaskDetailDto> GetPackagingSubTaskDetailAsync(string id, CancellationToken ct = default);
    Task<string?> PreviewAttachmentAsync(string attachmentUrl, long? expires = null, CancellationToken ct = default);
    Task<bool?> SavePackagingAsync(PackagingSaveRequestDto request, CancellationToken ct = default);
    Task<DeliveryOrderScanActualResultDto> ScanDeliveryActualAsync(DeliveryOrderScanActualRequestDto request, CancellationToken ct = default);
    Task<bool?> ConfirmDeliveryCompletionAsync(string deliveryOrderNo, CancellationToken ct = default);
    Task<RawMaterialReceivingDetailDto> GetRawMaterialReceivingDetailAsync(string instockNo, CancellationToken ct = default);
    Task<BlankInstockDto> AddBlankInstockAsync(CancellationToken ct = default);
    Task<List<WarehouseInfoDto>> QueryWarehouseInfoAsync(CancellationToken ct = default);
    Task<List<WarehouseAreaDto>> QueryWarehouseAreaByWarehouseCodeAsync(string? warehouseCode, CancellationToken ct = default);
    Task<List<DictGroupDto>> GetRawMaterialReceivingDictListAsync(CancellationToken ct = default);
    Task<AttachmentDto> UploadAttachmentAsync(FileResult photo, string attachmentFolder, string attachmentLocation, CancellationToken ct = default);
    Task<RawMaterialOcrDto> RecognizeIncomingAsync(AttachmentDto fileInfo, string instockNo, CancellationToken ct = default);
    Task<bool?> SaveOcrIncomingImageAsync(SaveOcrIncomingImageRequestDto request, CancellationToken ct = default);
    Task<QrCodeInfoDto> QueryQrCodeInfoAsync(string? qsCode, CancellationToken ct = default);
    Task<bool?> CancelBlankInstockAsync(string id, CancellationToken ct = default);
    Task<bool?> QuickInstockAsync(QuickInstockRequestDto request, CancellationToken ct = default);
}

public sealed class WarehouseApi : IWarehouseApi
{
    private readonly HttpClient _http;
    private readonly string _rawMaterialReceivingListEndpoint;
    private readonly string _addBlankInstockEndpoint;
    private readonly string _rawMaterialReceivingDetailEndpoint;
    private readonly string _queryWarehouseInfoEndpoint;
    private readonly string _queryWarehouseAreaEndpoint;
    private readonly string _uploadAttachmentEndpoint;
    private readonly string _previewAttachmentEndpoint;
    private readonly string _ocrIncomingEndpoint;
    private readonly string _saveOcrIncomingImageEndpoint;
    private readonly string _queryQrCodeInfoEndpoint;
    private readonly string _dictListEndpoint;
    private readonly string _cancelBlankInstockEndpoint;
    private readonly string _quickInstockEndpoint;
    private readonly string _needDeliveryOrdersEndpoint;
    private readonly string _deliveryOrderDetailEndpoint;
    private readonly string _deliveryOrderScanActualEndpoint;
    private readonly string _confirmDeliveryCompletionEndpoint;
    private readonly string _deliveryOrderDictListEndpoint;
    private readonly string _packagingSubTaskListEndpoint;
    private readonly string _packagingSubTaskDetailEndpoint;
    private readonly string _packagingSaveEndpoint;
    private readonly string _workOrderDictListEndpoint;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public WarehouseApi(HttpClient http, IConfigLoader configLoader)
    {
        _http = http;
        var servicePath = _http.BaseAddress?.AbsolutePath?.TrimEnd('/') ?? "/jxhljszpService";
        _rawMaterialReceivingListEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("rawMaterialReceiving.listInStock", "/pda/rawMaterialReceiving/listInStock"), servicePath);
        _addBlankInstockEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("rawMaterialReceiving.addBlankInstock", "/pda/rawMaterialReceiving/addBlankInstock"), servicePath);
        _rawMaterialReceivingDetailEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("rawMaterialReceiving.queryDetailByInstockNo", "/pda/rawMaterialReceiving/queryDetailByInstockNo"), servicePath);
        _queryWarehouseInfoEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("rawMaterialReceiving.queryWarehouseInfo", "/pda/rawMaterialReceiving/queryWarehouseInfo"), servicePath);
        _queryWarehouseAreaEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("rawMaterialReceiving.queryWarehouseAreaByWarehouseCode", "/pda/rawMaterialReceiving/queryWarehouseAreaByWarehouseCode"), servicePath);
        _uploadAttachmentEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("attachment.uploadAttachment", "/pda/attachment/uploadAttachment"), servicePath);
        _previewAttachmentEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("attachment.previewAttachment", "/pda/attachment/previewAttachment"), servicePath);
        _ocrIncomingEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("rawMaterialReceiving.ocrIncoming", "/pda/rawMaterialReceiving/ocrIncoming"), servicePath);
        _saveOcrIncomingImageEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("rawMaterialReceiving.saveOcrIncomingImage", "/pda/rawMaterialReceiving/saveOcrIncomingImage"), servicePath);
        _queryQrCodeInfoEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("rawMaterialReceiving.queryQrCodeInfo", "/pda/rawMaterialReceiving/queryQrCodeInfo"), servicePath);
        _dictListEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("rawMaterialReceiving.getDictList", "/pda/rawMaterialReceiving/getDictList"), servicePath);
        _cancelBlankInstockEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("rawMaterialReceiving.cancelBlankInstock", "/pda/rawMaterialReceiving/cancelBlankInstock"), servicePath);
        _quickInstockEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("rawMaterialReceiving.quickInstock", "/pda/rawMaterialReceiving/quickInstock"), servicePath);
        _needDeliveryOrdersEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("deliveryOrder.listNeedDeliveryOrders", "/pda/deliveryOrder/listNeedDeliveryOrders"), servicePath);
        _deliveryOrderDetailEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("deliveryOrder.detail", "/pda/deliveryOrder/detail"), servicePath);
        _deliveryOrderScanActualEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("deliveryOrder.scanActual", "/pda/deliveryOrder/scanActual"), servicePath);
        _confirmDeliveryCompletionEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("deliveryOrder.confirmDeliveryCompletion", "/pda/deliveryOrder/confirmDeliveryCompletion"), servicePath);
        _deliveryOrderDictListEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("deliveryOrder.getDictList", "/pda/deliveryOrder/getDictList"), servicePath);
        _packagingSubTaskListEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("packagingSubTask.list", "/pda/pmsWorkOrder/getPackagingSubTaskList"), servicePath);
        _packagingSubTaskDetailEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("packagingSubTask.detail", "/pda/pmsWorkOrder/getPackagingSubTaskDetail"), servicePath);
        _packagingSaveEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("packagingSubTask.packingSave", "/pda/pmsWorkOrder/packingSave"), servicePath);
        _workOrderDictListEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("workOrder.dictList", "/pda/pmsWorkOrder/getWorkOrderDictList"), servicePath);
    }

    public async Task<List<RawMaterialReceivingDto>> GetRawMaterialReceivingListAsync(CancellationToken ct = default)
    {
        var instockStatusNames = await LoadInstockStatusNamesAsync(ct).ConfigureAwait(false);
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _rawMaterialReceivingListEndpoint);
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<List<RawMaterialReceivingDto>>>(stream, JsonOptions, ct).ConfigureAwait(false);
        if (data is null)
        {
            throw new InvalidOperationException("接口返回为空。");
        }
        if (data.success == false)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(data.message) ? "接口返回失败。" : data.message);
        }
        var list = data.result ?? new List<RawMaterialReceivingDto>();
        ApplyInstockStatusNames(list, instockStatusNames);
        return list;
    }


    public async Task<List<DeliveryOrderDto>> GetNeedDeliveryOrdersAsync(CancellationToken ct = default)
    {
        var auditStatusNames = await LoadDeliveryAuditStatusNamesAsync(ct).ConfigureAwait(false);
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _needDeliveryOrdersEndpoint);
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<List<DeliveryOrderDto>>(resp, ct).ConfigureAwait(false);
        var list = data.result ?? new List<DeliveryOrderDto>();
        ApplyDeliveryAuditStatusNames(list, auditStatusNames);
        return list;
    }


    public async Task<DeliveryOrderDetailDto> GetDeliveryOrderDetailAsync(string deliveryNo, CancellationToken ct = default)
    {
        var auditStatusNames = await LoadDeliveryAuditStatusNamesAsync(ct).ConfigureAwait(false);
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, BuildUrlWithQuery(_deliveryOrderDetailEndpoint, new Dictionary<string, string?>
        {
            [nameof(deliveryNo)] = deliveryNo
        }));
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<DeliveryOrderDetailDto>(resp, ct).ConfigureAwait(false);
        var detail = data.result ?? new DeliveryOrderDetailDto();
        ApplyDeliveryAuditStatusName(detail, auditStatusNames);
        return detail;
    }


    public async Task<List<PackagingSubTaskDto>> GetPackagingSubTaskListAsync(CancellationToken ct = default)
    {
        var workOrderStatusNames = await LoadWorkOrderStatusNamesAsync(ct).ConfigureAwait(false);
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _packagingSubTaskListEndpoint);
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<List<PackagingSubTaskDto>>(resp, ct).ConfigureAwait(false);
        var list = data.result ?? new List<PackagingSubTaskDto>();
        ApplyPackagingWorkOrderStatusNames(list, workOrderStatusNames);
        return list;
    }

    public async Task<PackagingSubTaskDetailDto> GetPackagingSubTaskDetailAsync(string id, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, BuildUrlWithQuery(_packagingSubTaskDetailEndpoint, new Dictionary<string, string?>
        {
            [nameof(id)] = id
        }));
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<PackagingSubTaskDetailDto>(resp, ct).ConfigureAwait(false);
        var detail = data.result ?? new PackagingSubTaskDetailDto();
        var dictNames = await LoadWorkOrderDictNamesAsync(ct).ConfigureAwait(false);
        ApplyPackagingDetailDictNames(detail, dictNames);
        return detail;
    }



    public async Task<string?> PreviewAttachmentAsync(string attachmentUrl, long? expires = null, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, BuildUrlWithQuery(_previewAttachmentEndpoint, new Dictionary<string, string?>
        {
            [nameof(attachmentUrl)] = attachmentUrl,
            [nameof(expires)] = expires?.ToString()
        }));
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<string?>(resp, ct).ConfigureAwait(false);
        return data.result;
    }

    public async Task<bool?> SavePackagingAsync(PackagingSaveRequestDto request, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _packagingSaveEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, request, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<bool?>(resp, ct).ConfigureAwait(false);
        return data.result;
    }

    public async Task<DeliveryOrderScanActualResultDto> ScanDeliveryActualAsync(DeliveryOrderScanActualRequestDto request, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _deliveryOrderScanActualEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, request, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<DeliveryOrderScanActualResultDto>(resp, ct).ConfigureAwait(false);
        return data.result ?? new DeliveryOrderScanActualResultDto();
    }


    public async Task<bool?> ConfirmDeliveryCompletionAsync(string deliveryOrderNo, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _confirmDeliveryCompletionEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, new { deliveryOrderNo }, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<bool?>(resp, ct).ConfigureAwait(false);
        return data.result;
    }

    public async Task<RawMaterialReceivingDetailDto> GetRawMaterialReceivingDetailAsync(string instockNo, CancellationToken ct = default)
    {
        var instockStatusNames = await LoadInstockStatusNamesAsync(ct).ConfigureAwait(false);
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, BuildUrlWithQuery(_rawMaterialReceivingDetailEndpoint, new Dictionary<string, string?>
        {
            [nameof(instockNo)] = instockNo
        }));
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<RawMaterialReceivingDetailDto>(resp, ct).ConfigureAwait(false);
        var detail = data.result ?? new RawMaterialReceivingDetailDto();
        ApplyInstockStatusName(detail, instockStatusNames);
        return detail;
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

    public async Task<List<WarehouseAreaDto>> QueryWarehouseAreaByWarehouseCodeAsync(string? warehouseCode, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, BuildUrlWithQuery(_queryWarehouseAreaEndpoint, new Dictionary<string, string?>
        {
            [nameof(warehouseCode)] = warehouseCode
        }));
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<List<WarehouseAreaDto>>(resp, ct).ConfigureAwait(false);
        return data.result ?? new List<WarehouseAreaDto>();
    }

    public async Task<List<DictGroupDto>> GetRawMaterialReceivingDictListAsync(CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _dictListEndpoint);
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<List<DictGroupDto>>(resp, ct).ConfigureAwait(false);
        return data.result ?? new List<DictGroupDto>();
    }

    public async Task<AttachmentDto> UploadAttachmentAsync(FileResult photo, string attachmentFolder, string attachmentLocation, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _uploadAttachmentEndpoint);
        await using var stream = await photo.OpenReadAsync().ConfigureAwait(false);
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(stream);
        var contentType = string.IsNullOrWhiteSpace(photo.ContentType) ? "image/jpeg" : photo.ContentType;
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", photo.FileName);
        content.Add(new StringContent(attachmentFolder), nameof(attachmentFolder));
        content.Add(new StringContent(attachmentLocation), nameof(attachmentLocation));
        using var resp = await _http.PostAsync(url, content, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var data = JsonSerializer.Deserialize<ApiResp<AttachmentDto>>(json, JsonOptions);
        if (data is null)
        {
            throw new InvalidOperationException("接口返回为空。");
        }
        if (data.success == false)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(data.message) ? "接口返回失败。" : data.message);
        }
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
        await EnsureSuccessStatusCodeWithBodyAsync(resp, ct).ConfigureAwait(false);
        var data = await ReadApiResponseAsync<RawMaterialOcrDto>(resp, ct).ConfigureAwait(false);
        return data.result ?? new RawMaterialOcrDto();
    }

    public async Task<bool?> SaveOcrIncomingImageAsync(SaveOcrIncomingImageRequestDto request, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _saveOcrIncomingImageEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, request, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<bool?>(resp, ct).ConfigureAwait(false);
        return data.result;
    }

    public async Task<QrCodeInfoDto> QueryQrCodeInfoAsync(string? qsCode, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, BuildUrlWithQuery(_queryQrCodeInfoEndpoint, new Dictionary<string, string?>
        {
            [nameof(qsCode)] = qsCode
        }));
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<QrCodeInfoDto>(resp, ct).ConfigureAwait(false);
        return data.result ?? new QrCodeInfoDto();
    }


    public async Task<bool?> CancelBlankInstockAsync(string id, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, BuildUrlWithQuery(_cancelBlankInstockEndpoint, new Dictionary<string, string?>
        {
            [nameof(id)] = id
        }));
        using var resp = await _http.PostAsync(url, new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()), ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<bool?>(resp, ct).ConfigureAwait(false);
        return data.result;
    }

    public async Task<bool?> QuickInstockAsync(QuickInstockRequestDto request, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _quickInstockEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, request, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<bool?>(resp, ct).ConfigureAwait(false);
        return data.result;
    }


    private static void ApplyPackagingWorkOrderStatusNames(IEnumerable<PackagingSubTaskDto> items, IReadOnlyDictionary<string, string> workOrderStatusNames)
    {
        foreach (var item in items)
        {
            item.workOrderStatus = MapStatusName(item.workOrderStatus, workOrderStatusNames);
        }
    }

    private static void ApplyPackagingDetailDictNames(PackagingSubTaskDetailDto detail, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> dictNames)
    {
        detail.workOrderStatus = MapDictName(detail.workOrderStatus, dictNames, "workOrderStatus");
        detail.materialProperty = MapDictName(detail.materialProperty, dictNames, "materialProperty");
        detail.packageMethod = MapDictName(detail.packageMethod, dictNames, "packageMethod");
        detail.packageWeight = MapDictName(detail.packageWeight, dictNames, "packageWeight");
        detail.packagingClothColor = MapDictName(detail.packagingClothColor, dictNames, "packagingClothColor");
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadWorkOrderStatusNamesAsync(CancellationToken ct)
    {
        var dictNames = await LoadWorkOrderDictNamesAsync(ct).ConfigureAwait(false);
        return dictNames.TryGetValue("workOrderStatus", out var statusNames) ? statusNames : new Dictionary<string, string>();
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> LoadWorkOrderDictNamesAsync(CancellationToken ct)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _workOrderDictListEndpoint);
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<List<DictGroupDto>>(resp, ct).ConfigureAwait(false);
        return data.result?
            .Where(group => !string.IsNullOrWhiteSpace(group.field))
            .GroupBy(group => group.field!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<string, string>)group
                    .SelectMany(item => item.dictItems ?? new List<DictItemDto>())
                    .Where(item => !string.IsNullOrWhiteSpace(item.dictItemValue))
                    .GroupBy(item => item.dictItemValue!)
                    .ToDictionary(itemGroup => itemGroup.Key, itemGroup => itemGroup.First().dictItemName ?? itemGroup.Key),
                StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    }

    private static string? MapStatusName(string? status, IReadOnlyDictionary<string, string> statusNames)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return status;
        }

        return statusNames.TryGetValue(status, out var name) ? name : status;
    }

    private static string? MapDictName(string? value, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> dictNames, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || !dictNames.TryGetValue(field, out var itemNames))
        {
            return value;
        }

        return itemNames.TryGetValue(value, out var name) ? name : value;
    }

    private static void ApplyDeliveryAuditStatusNames(IEnumerable<DeliveryOrderDto> items, IReadOnlyDictionary<string, string> auditStatusNames)
    {
        foreach (var item in items)
        {
            if (!string.IsNullOrWhiteSpace(item.auditStatus) && auditStatusNames.TryGetValue(item.auditStatus, out var name))
            {
                item.auditStatusName = name;
            }
        }
    }

    private static void ApplyDeliveryAuditStatusName(DeliveryOrderDetailDto detail, IReadOnlyDictionary<string, string> auditStatusNames)
    {
        if (!string.IsNullOrWhiteSpace(detail.auditStatus) && auditStatusNames.TryGetValue(detail.auditStatus, out var name))
        {
            detail.auditStatusName = name;
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadDeliveryAuditStatusNamesAsync(CancellationToken ct)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _deliveryOrderDictListEndpoint);
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<List<DictGroupDto>>(resp, ct).ConfigureAwait(false);
        return data.result?
            .FirstOrDefault(group => string.Equals(group.field, "auditStatus", StringComparison.OrdinalIgnoreCase))?
            .dictItems?
            .Where(item => !string.IsNullOrWhiteSpace(item.dictItemValue))
            .GroupBy(item => item.dictItemValue!)
            .ToDictionary(group => group.Key, group => group.First().dictItemName ?? group.Key)
            ?? new Dictionary<string, string>();
    }

    private static void ApplyInstockStatusNames(IEnumerable<RawMaterialReceivingDto> items, IReadOnlyDictionary<string, string> instockStatusNames)
    {
        foreach (var item in items)
        {
            if (!string.IsNullOrWhiteSpace(item.instockStatus) && instockStatusNames.TryGetValue(item.instockStatus, out var name))
            {
                item.instockStatusName = name;
            }
        }
    }

    private static void ApplyInstockStatusName(RawMaterialReceivingDetailDto detail, IReadOnlyDictionary<string, string> instockStatusNames)
    {
        if (!string.IsNullOrWhiteSpace(detail.instockStatus) && instockStatusNames.TryGetValue(detail.instockStatus, out var name))
        {
            detail.instockStatusName = name;
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadInstockStatusNamesAsync(CancellationToken ct)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _dictListEndpoint);
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<List<DictGroupDto>>(resp, ct).ConfigureAwait(false);
        return data.result?
            .FirstOrDefault(group => string.Equals(group.field, "instockStatus", StringComparison.OrdinalIgnoreCase))?
            .dictItems?
            .Where(item => !string.IsNullOrWhiteSpace(item.dictItemValue))
            .GroupBy(item => item.dictItemValue!)
            .ToDictionary(group => group.Key, group => group.First().dictItemName ?? group.Key)
            ?? new Dictionary<string, string>();
    }

    private static async Task EnsureSuccessStatusCodeWithBodyAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode)
        {
            return;
        }

        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        throw new HttpRequestException(
            $"接口返回HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}。响应内容：{TrimResponseBody(body)}",
            null,
            resp.StatusCode);
    }

    private static async Task<ApiResp<T>> ReadApiResponseAsync<T>(HttpResponseMessage resp, CancellationToken ct)
    {
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var data = JsonSerializer.Deserialize<ApiResp<T>>(body, JsonOptions);
        if (data is null)
        {
            throw new InvalidOperationException($"接口返回为空。响应内容：{TrimResponseBody(body)}");
        }
        if (data.success == false)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(data.message) ? "接口返回失败。" : data.message);
        }
        return data;
    }

    private static string TrimResponseBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "<空>";
        }

        const int maxLength = 1000;
        var trimmed = body.Trim();
        return trimmed.Length <= maxLength ? trimmed : $"{trimmed[..maxLength]}...";
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
