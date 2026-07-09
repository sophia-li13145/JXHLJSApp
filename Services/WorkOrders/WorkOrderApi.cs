using JXHLJSApp.Models;
using JXHLJSApp.Models.WorkOrders;
using JXHLJSApp.Models.Warehouse;
using JXHLJSApp.Services.Common;
using System.Net.Http.Json;
using System.Text.Json;

namespace JXHLJSApp.Services.WorkOrders;

public interface IWorkOrderApi
{
    Task<List<WorkOrderTaskDto>> GetWorkOrderListAsync(string? deviceCode = null, string? machineNo = null, string? workOrderStatus = null, CancellationToken ct = default);
    Task<bool> BindWorkerMachineAsync(string devCode, CancellationToken ct = default);
    Task<bool> ScanToWorkAsync(string devCode, string workOrderNo, CancellationToken ct = default);
    Task<List<WorkOrderTaskDto>> GetCurrentUserMachinesWorkOrdersAsync(CancellationToken ct = default);
    Task<bool> StartWorkOrderAsync(string workOrderNo, CancellationToken ct = default);
    Task<WorkOrderDetailDto?> GetWorkOrderDetailAsync(string id, CancellationToken ct = default);
    Task<List<WorkOrderDetailDto>> GetCurrentTaskPoolAsync(string workOrderNo, CancellationToken ct = default);
    Task<List<WorkOrderInputOutputDto>> GetWorkOrderInputOutputAsync(string workOrderNo, CancellationToken ct = default);
    Task<MaterialQrCodeInfoDto> ScanQueryMaterialInfoAsync(string qrCode, CancellationToken ct = default);
    Task<bool> ConfirmMaterialInputAsync(MaterialInputConfirmDto input, CancellationToken ct = default);
    Task<bool> ConfirmMaterialOutputAsync(MaterialOutputConfirmDto output, CancellationToken ct = default);
    Task<List<WorkOrderAbnormalOptionDto>> GetAbnormalTypeOptionsAsync(CancellationToken ct = default);
    Task<List<WorkOrderAbnormalOptionDto>> GetReworkReasonOptionsAsync(CancellationToken ct = default);
    Task<MaterialQrCodeInfoDto> ScanAbnormalMaterialAsync(string qrCode, CancellationToken ct = default);
    Task<MaterialQrCodeInfoDto> ScanReworkMaterialAsync(string qrCode, CancellationToken ct = default);
    Task<AttachmentDto> UploadAbnormalAttachmentAsync(FileResult photo, CancellationToken ct = default);
    Task<AttachmentDto> UploadReworkAttachmentAsync(FileResult photo, CancellationToken ct = default);
    Task<bool> AddAbnormalRecordAsync(WorkOrderAbnormalAddRequestDto request, CancellationToken ct = default);
    Task<bool> ConfirmCompletionAsync(string workOrderNo, CancellationToken ct = default);
    Task<bool> StopWorkOrderAsync(string workOrderNo, CancellationToken ct = default);
    Task<ProductionStatisticsDto?> GetProductionStatisticsAsync(string date, CancellationToken ct = default);
}

public sealed class WorkOrderApi : IWorkOrderApi
{
    private readonly HttpClient _http;
    private readonly string _listEndpoint;
    private readonly string _bindWorkerMachineEndpoint;
    private readonly string _scanToWorkEndpoint;
    private readonly string _currentUserMachinesWorkOrderEndpoint;
    private readonly string _orderStartEndpoint;
    private readonly string _detailEndpoint;
    private readonly string _currentTaskPoolEndpoint;
    private readonly string _inputOutputEndpoint;
    private readonly string _dictListEndpoint;
    private readonly string _materialQrCodeEndpoint;
    private readonly string _confirmInputEndpoint;
    private readonly string _confirmOutputEndpoint;
    private readonly string _abnormalDictListEndpoint;
    private readonly string _abnormalAddEndpoint;
    private readonly string _abnormalScanQrCodeEndpoint;
    private readonly string _reworkScanQrCodeEndpoint;
    private readonly string _uploadAttachmentEndpoint;
    private readonly string _confirmCompletionEndpoint;
    private readonly string _orderStopEndpoint;
    private readonly string _productionStatisticsEndpoint;
    private IReadOnlyDictionary<string, string>? _workOrderStatusNames;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public WorkOrderApi(HttpClient http, IConfigLoader configLoader)
    {
        _http = http;
        var servicePath = _http.BaseAddress?.AbsolutePath?.TrimEnd('/') ?? "/jxhljszpService";
        _listEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("workOrder.list", "/pda/pmsWorkOrder/getWorkOrderList"), servicePath);
        _bindWorkerMachineEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("workOrder.bindWorkerMachine", "/pda/devUserMachineBindRecord/workerBindDev"), servicePath);
        _scanToWorkEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("machineBind.scanToWork", "/pda/devUserMachineBindRecord/scanToWork"), servicePath);
        _currentUserMachinesWorkOrderEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("workOrder.currentUserMachinesWorkOrder", "/pda/pmsWorkOrder/currentUserMachinesWorkOrder"), servicePath);
        _orderStartEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("workOrder.orderStart", "/pda/pmsWorkOrder/orderStart"), servicePath);
        _detailEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("workOrder.detail", "/pda/pmsWorkOrder/getWorkOrderDetail"), servicePath);
        _currentTaskPoolEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("workOrder.currentTaskPool", "/pda/pmsWorkOrder/getCurrentTaskPool"), servicePath);
        _inputOutputEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("workOrder.inputOutput", "/pda/pmsWorkOrder/getWorkOrderInputOutput"), servicePath);
        _dictListEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("workOrder.dictList", "/pda/pmsWorkOrder/getWorkOrderDictList"), servicePath);
        _materialQrCodeEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("materialQrCode.scanQueryMaterialInfo", "/pda/pmsWorkOrder/scanInputQrCode"), servicePath);
        _confirmInputEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("workOrder.confirmInput", "/pda/pmsWorkOrder/confirmInput"), servicePath);
        _confirmOutputEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("workOrder.confirmOutput", "/pda/pmsWorkOrder/confirmOutput"), servicePath);
        _abnormalDictListEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("workOrderAbnormalRecord.getDictList", "/pda/qsWorkOrderAbnormalRecord/getDictList"), servicePath);
        _abnormalAddEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("workOrderAbnormalRecord.add", "/pda/qsWorkOrderAbnormalRecord/add"), servicePath);
        _abnormalScanQrCodeEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("workOrderAbnormalRecord.scanQrCode", "/pda/pmsWorkOrder/scanAbnormalReportQrCode"), servicePath);
        _reworkScanQrCodeEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("workOrderAbnormalRecord.scanReworkQrCode", "/pda/pmsWorkOrder/scanReworkReportQrCode"), servicePath);
        _uploadAttachmentEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("attachment.uploadAttachment", "/pda/attachment/uploadAttachment"), servicePath);
        _confirmCompletionEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("workOrder.confirmCompletion", "/pda/pmsWorkOrder/confirmCompletion"), servicePath);
        _orderStopEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("workOrder.orderStop", "/pda/pmsWorkOrder/orderStop"), servicePath);
        _productionStatisticsEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("workOrder.productionStatistics", "/pda/pmsWorkOrder/getProductionStatistics"), servicePath);
    }

    public async Task<List<WorkOrderTaskDto>> GetWorkOrderListAsync(string? deviceCode = null, string? machineNo = null, string? workOrderStatus = null, CancellationToken ct = default)
    {
        var query = new Dictionary<string, string?>
        {
            [nameof(deviceCode)] = deviceCode,
            [nameof(machineNo)] = machineNo,
            [nameof(workOrderStatus)] = workOrderStatus
        };

        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, BuildUrlWithQuery(_listEndpoint, query));
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<List<WorkOrderTaskDto>>>(stream, JsonOptions, ct).ConfigureAwait(false);
        EnsureApiSuccess(data);
        var orders = data?.result ?? new List<WorkOrderTaskDto>();
        await ApplyWorkOrderStatusNamesAsync(orders, ct).ConfigureAwait(false);
        return orders;
    }

    public async Task<bool> BindWorkerMachineAsync(string devCode, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _bindWorkerMachineEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, new { devCode }, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<JsonElement?>>(stream, JsonOptions, ct).ConfigureAwait(false);
        EnsureApiSuccess(data);
        return ReadFlexibleBooleanResult(data);
    }

    public async Task<bool> ScanToWorkAsync(string devCode, string workOrderNo, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _scanToWorkEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, new { devCode, workOrderNo }, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<JsonElement?>>(stream, JsonOptions, ct).ConfigureAwait(false);
        EnsureApiSuccess(data);
        return ReadFlexibleBooleanResult(data);
    }


    public async Task<List<WorkOrderTaskDto>> GetCurrentUserMachinesWorkOrdersAsync(CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _currentUserMachinesWorkOrderEndpoint);
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<List<WorkOrderTaskDto>>>(stream, JsonOptions, ct).ConfigureAwait(false);
        EnsureApiSuccess(data);
        var orders = data?.result ?? new List<WorkOrderTaskDto>();
        await ApplyWorkOrderStatusNamesAsync(orders, ct).ConfigureAwait(false);
        return orders;
    }

    public async Task<bool> StartWorkOrderAsync(string workOrderNo, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _orderStartEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, new { workOrderNo }, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<JsonElement?>>(stream, JsonOptions, ct).ConfigureAwait(false);
        EnsureApiSuccess(data);
        return ReadFlexibleBooleanResult(data);
    }


    public async Task<WorkOrderDetailDto?> GetWorkOrderDetailAsync(string id, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, BuildUrlWithQuery(_detailEndpoint, new Dictionary<string, string?>
        {
            [nameof(id)] = id
        }));
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<WorkOrderDetailDto>>(stream, JsonOptions, ct).ConfigureAwait(false);
        EnsureApiSuccess(data);
        var detail = data?.result;
        await ApplyWorkOrderStatusNameAsync(detail, ct).ConfigureAwait(false);
        return detail;
    }


    public async Task<List<WorkOrderDetailDto>> GetCurrentTaskPoolAsync(string workOrderNo, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, BuildUrlWithQuery(_currentTaskPoolEndpoint, new Dictionary<string, string?>
        {
            [nameof(workOrderNo)] = workOrderNo
        }));
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<JsonElement?>>(stream, JsonOptions, ct).ConfigureAwait(false);
        EnsureApiSuccess(data);
        var tasks = ReadWorkOrderDetailResult(data);
        await ApplyWorkOrderStatusNamesAsync(tasks, ct).ConfigureAwait(false);
        return tasks;
    }


    public async Task<List<WorkOrderInputOutputDto>> GetWorkOrderInputOutputAsync(string workOrderNo, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, BuildUrlWithQuery(_inputOutputEndpoint, new Dictionary<string, string?>
        {
            [nameof(workOrderNo)] = workOrderNo
        }));
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<JsonElement?>>(stream, JsonOptions, ct).ConfigureAwait(false);
        EnsureApiSuccess(data);
        var tasks = ReadInputOutputResult(data);
        await ApplyWorkOrderStatusNamesAsync(tasks, ct).ConfigureAwait(false);
        return tasks;
    }


    public async Task<MaterialQrCodeInfoDto> ScanQueryMaterialInfoAsync(string qrCode, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, BuildUrlWithQuery(_materialQrCodeEndpoint, new Dictionary<string, string?>
        {
            [nameof(qrCode)] = qrCode
        }));
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<MaterialQrCodeInfoDto>>(stream, JsonOptions, ct).ConfigureAwait(false);
        EnsureApiSuccess(data);
        return data?.result ?? new MaterialQrCodeInfoDto();
    }


    public async Task<bool> ConfirmMaterialInputAsync(MaterialInputConfirmDto input, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _confirmInputEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, input, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<JsonElement?>>(stream, JsonOptions, ct).ConfigureAwait(false);
        EnsureApiSuccess(data);
        return ReadFlexibleBooleanResult(data);
    }

    public async Task<bool> ConfirmMaterialOutputAsync(MaterialOutputConfirmDto output, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _confirmOutputEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, output, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<JsonElement?>>(stream, JsonOptions, ct).ConfigureAwait(false);
        EnsureApiSuccess(data);
        return ReadFlexibleBooleanResult(data);
    }


    public Task<List<WorkOrderAbnormalOptionDto>> GetAbnormalTypeOptionsAsync(CancellationToken ct = default) =>
        GetAbnormalRecordOptionsAsync("abnormalType", ct);

    public Task<List<WorkOrderAbnormalOptionDto>> GetReworkReasonOptionsAsync(CancellationToken ct = default) =>
        GetAbnormalRecordOptionsAsync("reworkReason", ct);

    private async Task<List<WorkOrderAbnormalOptionDto>> GetAbnormalRecordOptionsAsync(string field, CancellationToken ct)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _abnormalDictListEndpoint);
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<List<WorkOrderDictDto>>>(stream, JsonOptions, ct).ConfigureAwait(false);
        EnsureApiSuccess(data);
        return data?.result?
            .FirstOrDefault(dict => string.Equals(dict.field, field, StringComparison.OrdinalIgnoreCase))?
            .dictItems?
            .Where(item => !string.IsNullOrWhiteSpace(item.dictItemValue) && !string.IsNullOrWhiteSpace(item.dictItemName))
            .Select(item => new WorkOrderAbnormalOptionDto { value = item.dictItemValue, name = item.dictItemName })
            .ToList() ?? new List<WorkOrderAbnormalOptionDto>();
    }

    public Task<MaterialQrCodeInfoDto> ScanAbnormalMaterialAsync(string qrCode, CancellationToken ct = default) =>
        ScanMaterialAsync(_abnormalScanQrCodeEndpoint, qrCode, ct);

    public Task<MaterialQrCodeInfoDto> ScanReworkMaterialAsync(string qrCode, CancellationToken ct = default) =>
        ScanMaterialAsync(_reworkScanQrCodeEndpoint, qrCode, ct);

    private async Task<MaterialQrCodeInfoDto> ScanMaterialAsync(string endpoint, string qrCode, CancellationToken ct)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, BuildUrlWithQuery(endpoint, new Dictionary<string, string?>
        {
            [nameof(qrCode)] = qrCode
        }));
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<MaterialQrCodeInfoDto>>(stream, JsonOptions, ct).ConfigureAwait(false);
        EnsureApiSuccess(data);
        return data?.result ?? new MaterialQrCodeInfoDto();
    }

    public Task<AttachmentDto> UploadAbnormalAttachmentAsync(FileResult photo, CancellationToken ct = default) =>
        UploadAttachmentAsync(photo, "abnormalRecord", "images", ct);

    public Task<AttachmentDto> UploadReworkAttachmentAsync(FileResult photo, CancellationToken ct = default) =>
        UploadAttachmentAsync(photo, "toolingManager", "images", ct);

    private async Task<AttachmentDto> UploadAttachmentAsync(FileResult photo, string attachmentFolder, string attachmentLocation, CancellationToken ct)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _uploadAttachmentEndpoint);
        await using var stream = await photo.OpenReadAsync().ConfigureAwait(false);
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(string.IsNullOrWhiteSpace(photo.ContentType) ? "image/jpeg" : photo.ContentType);
        content.Add(fileContent, "file", photo.FileName);
        content.Add(new StringContent(attachmentFolder), "attachmentFolder");
        content.Add(new StringContent(attachmentLocation), "attachmentLocation");
        using var resp = await _http.PostAsync(url, content, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var respStream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<AttachmentDto>>(respStream, JsonOptions, ct).ConfigureAwait(false);
        EnsureApiSuccess(data);
        return data?.result ?? new AttachmentDto { attachmentFolder = attachmentFolder, attachmentLocation = attachmentLocation, attachmentName = photo.FileName, attachmentRealName = photo.FileName };
    }

    public async Task<bool> AddAbnormalRecordAsync(WorkOrderAbnormalAddRequestDto request, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _abnormalAddEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, request, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<JsonElement?>>(stream, JsonOptions, ct).ConfigureAwait(false);
        EnsureApiSuccess(data);
        return ReadFlexibleBooleanResult(data);
    }

    public async Task<bool> ConfirmCompletionAsync(string workOrderNo, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _confirmCompletionEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, new { workOrderNo }, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<JsonElement?>>(stream, JsonOptions, ct).ConfigureAwait(false);
        EnsureApiSuccess(data);
        return ReadFlexibleBooleanResult(data);
    }



    public async Task<bool> StopWorkOrderAsync(string workOrderNo, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _orderStopEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, new { workOrderNo }, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<JsonElement?>>(stream, JsonOptions, ct).ConfigureAwait(false);
        EnsureApiSuccess(data);
        return ReadFlexibleBooleanResult(data);
    }

    private static bool ReadFlexibleBooleanResult(ApiResp<JsonElement?>? response)
    {
        if (response?.result is not { } result)
        {
            return response?.success == true;
        }

        return result.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => result.TryGetInt32(out var value) && value != 0,
            JsonValueKind.String => bool.TryParse(result.GetString(), out var value)
                ? value
                : response?.success == true,
            JsonValueKind.Null or JsonValueKind.Undefined => response?.success == true,
            _ => response?.success == true
        };
    }




    public async Task<ProductionStatisticsDto?> GetProductionStatisticsAsync(string date, CancellationToken ct = default)
    {
        var query = new Dictionary<string, string?>
        {
            [nameof(date)] = date
        };

        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, BuildUrlWithQuery(_productionStatisticsEndpoint, query));
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<ProductionStatisticsDto>>(stream, JsonOptions, ct).ConfigureAwait(false);
        EnsureApiSuccessOrCodeZero(data);
        return data?.result;
    }

    private async Task ApplyWorkOrderStatusNamesAsync(IEnumerable<WorkOrderTaskDto> orders, CancellationToken ct)
    {
        var statusNames = await GetWorkOrderStatusNamesAsync(ct).ConfigureAwait(false);
        foreach (var order in orders)
        {
            order.workOrderStatus = MapWorkOrderStatus(order.workOrderStatus, statusNames);
        }
    }

    private async Task ApplyWorkOrderStatusNamesAsync(IEnumerable<WorkOrderDetailDto> tasks, CancellationToken ct)
    {
        var statusNames = await GetWorkOrderStatusNamesAsync(ct).ConfigureAwait(false);
        foreach (var task in tasks)
        {
            task.workOrderStatus = MapWorkOrderStatus(task.workOrderStatus, statusNames);
        }
    }

    private async Task ApplyWorkOrderStatusNamesAsync(IEnumerable<WorkOrderInputOutputDto> tasks, CancellationToken ct)
    {
        var statusNames = await GetWorkOrderStatusNamesAsync(ct).ConfigureAwait(false);
        foreach (var task in tasks)
        {
            task.workOrderStatus = MapWorkOrderStatus(task.workOrderStatus, statusNames);
        }
    }

    private async Task ApplyWorkOrderStatusNameAsync(WorkOrderDetailDto? detail, CancellationToken ct)
    {
        if (detail is null) return;

        var statusNames = await GetWorkOrderStatusNamesAsync(ct).ConfigureAwait(false);
        detail.workOrderStatus = MapWorkOrderStatus(detail.workOrderStatus, statusNames);
    }

    private async Task<IReadOnlyDictionary<string, string>> GetWorkOrderStatusNamesAsync(CancellationToken ct)
    {
        if (_workOrderStatusNames is not null)
        {
            return _workOrderStatusNames;
        }

        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _dictListEndpoint);
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<List<WorkOrderDictDto>>>(stream, JsonOptions, ct).ConfigureAwait(false);
        EnsureApiSuccess(data);

        _workOrderStatusNames = data?.result?
            .FirstOrDefault(dict => string.Equals(dict.field, "workOrderStatus", StringComparison.OrdinalIgnoreCase))?
            .dictItems?
            .Where(item => !string.IsNullOrWhiteSpace(item.dictItemValue) && !string.IsNullOrWhiteSpace(item.dictItemName))
            .ToDictionary(item => item.dictItemValue!, item => item.dictItemName!)
            ?? new Dictionary<string, string>();

        return _workOrderStatusNames;
    }

    private static string? MapWorkOrderStatus(string? status, IReadOnlyDictionary<string, string> statusNames)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return status;
        }

        return statusNames.TryGetValue(status, out var name) ? name : status;
    }


    private static List<WorkOrderDetailDto> ReadWorkOrderDetailResult(ApiResp<JsonElement?>? response)
    {
        if (response?.result is not { } result || result.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return new List<WorkOrderDetailDto>();
        }

        if (result.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<WorkOrderDetailDto>>(result.GetRawText(), JsonOptions) ?? new List<WorkOrderDetailDto>();
        }

        if (result.ValueKind == JsonValueKind.Object)
        {
            var item = JsonSerializer.Deserialize<WorkOrderDetailDto>(result.GetRawText(), JsonOptions);
            return item is null ? new List<WorkOrderDetailDto>() : new List<WorkOrderDetailDto> { item };
        }

        return new List<WorkOrderDetailDto>();
    }

    private static List<WorkOrderInputOutputDto> ReadInputOutputResult(ApiResp<JsonElement?>? response)
    {
        if (response?.result is not { } result || result.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return new List<WorkOrderInputOutputDto>();
        }

        if (result.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<WorkOrderInputOutputDto>>(result.GetRawText(), JsonOptions) ?? new List<WorkOrderInputOutputDto>();
        }

        if (result.ValueKind == JsonValueKind.Object)
        {
            var item = JsonSerializer.Deserialize<WorkOrderInputOutputDto>(result.GetRawText(), JsonOptions);
            return item is null ? new List<WorkOrderInputOutputDto>() : new List<WorkOrderInputOutputDto> { item };
        }

        return new List<WorkOrderInputOutputDto>();
    }

    private static void EnsureApiSuccess<T>(ApiResp<T>? response)
    {
        if (response?.success == true) return;

        var message = response?.message;
        if (string.IsNullOrWhiteSpace(message))
        {
            message = "接口返回失败，请稍后重试。";
        }

        throw new WorkOrderApiException(message);
    }

    private static void EnsureApiSuccessOrCodeZero<T>(ApiResp<T>? response)
    {
        if (response?.success == true || response?.code == 0) return;

        var message = response?.message;
        if (string.IsNullOrWhiteSpace(message))
        {
            message = "接口返回失败，请稍后重试。";
        }

        throw new WorkOrderApiException(message);
    }

    private static string BuildUrlWithQuery(string endpoint, IReadOnlyDictionary<string, string?> query)
    {
        var pairs = query
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}");
        var qs = string.Join("&", pairs);
        return string.IsNullOrEmpty(qs) ? endpoint : $"{endpoint}?{qs}";
    }
}


public sealed class WorkOrderApiException : Exception
{
    public WorkOrderApiException(string message) : base(message)
    {
    }
}


public sealed class MaterialInputConfirmDto
{
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    public string? qrCode { get; set; }
    public string? spec { get; set; }
    public string? stockBatch { get; set; }
    public string? workOrderCode { get; set; }
}

public sealed class MaterialOutputConfirmDto
{
    public decimal? outputLength { get; set; }
    public decimal? pieceWeight { get; set; }
    public string? productInspectStatus { get; set; }
    public string? qrCode { get; set; }
    public string? workOrderNo { get; set; }
}
