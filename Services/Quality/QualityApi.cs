using JXHLJSApp.Models;
using JXHLJSApp.Models.Quality;
using JXHLJSApp.Models.Warehouse;
using JXHLJSApp.Services.Common;
using System.Net.Http.Json;
using System.Text.Json;

namespace JXHLJSApp.Services.Quality;

public interface IQualityApi
{
    Task<List<IncomingQualityStatusFilter>> GetIncomingQualityStatusFiltersAsync(CancellationToken ct = default);
    Task<List<IncomingQualityOrderDto>> GetIncomingQualityOrdersAsync(string? status, CancellationToken ct = default);
    Task<IncomingQualityOrderDetailDto> GetIncomingQualityOrderDetailAsync(string incomingQualityNo, CancellationToken ct = default);
    Task<IncomingQualityScanMaterialDto> ScanIncomingQualityMaterialAsync(string qrCode, CancellationToken ct = default);
    Task<List<QualityDictOption>> GetInspectResultOptionsAsync(CancellationToken ct = default);
    Task<List<QualityDictOption>> GetProblemPointOptionsAsync(CancellationToken ct = default);
    Task<bool> SaveIncomingQualityResultAsync(IncomingQualitySaveResultRequestDto request, CancellationToken ct = default);
    Task<bool> CompleteIncomingQualityOrderAsync(string incomingQualityNo, CancellationToken ct = default);
    Task<bool> DeleteIncomingQualityOrderAsync(string incomingQualityNo, CancellationToken ct = default);
    Task<List<ProductionQualityOrderDto>> GetProductionQualityOrdersByResourceAsync(string resourceCode, CancellationToken ct = default);
    Task<List<ProductionQualityOrderDto>> GetProductionQualityOrdersAsync(string? resourceName, string? inspectStatus, string? qualityNo = null, CancellationToken ct = default);
    Task<ProductionQualityDetailDto> GetProductionQualityDetailAsync(string qualityNo, string workOrderNo, CancellationToken ct = default);
    Task<ProductionQualityDetailDto> CreateManualInspectionAsync(string qrCode, CancellationToken ct = default);
    Task<ProductionQualityDetailDto> GetManualInspectionDetailAsync(string qualityNo, CancellationToken ct = default);
    Task<bool> SaveManualInspectionResultAsync(ProductionManualInspectionSaveResultRequestDto request, CancellationToken ct = default);
    Task<ProductionQualityDetailDto> CompleteManualInspectionAsync(string qualityNo, CancellationToken ct = default);
    Task<ProductionQualityScanMaterialDto> ScanProductionQualityMaterialAsync(ProductionQualityScanMaterialRequestDto request, CancellationToken ct = default);
    Task<bool> CommitProductionQualityAsync(ProductionQualityCommitRequestDto request, CancellationToken ct = default);
    Task<bool> CommitProductionFirstInspectionAsync(ProductionFirstInspectionCommitRequestDto request, CancellationToken ct = default);
    Task<bool> CommitProductionPicklingAsync(ProductionPicklingCommitRequestDto request, CancellationToken ct = default);
    Task<bool> CommitProductionSamplingOrFullAsync(ProductionSamplingOrFullCommitRequestDto request, CancellationToken ct = default);
    Task<bool> CompleteProductionSamplingOrFullAsync(ProductionSamplingOrFullCompleteRequestDto request, CancellationToken ct = default);
}

public sealed class QualityApi : IQualityApi
{
    private readonly HttpClient _http;
    private readonly string _incomingQualityDictListEndpoint;
    private readonly string _incomingQualityListEndpoint;
    private readonly string _incomingQualityDetailEndpoint;
    private readonly string _incomingQualityScanEndpoint;
    private readonly string _incomingQualitySaveResultEndpoint;
    private readonly string _incomingQualityCompletedEndpoint;
    private readonly string _incomingQualityDeleteEndpoint;
    private readonly string _productionQualityListEndpoint;
    private readonly string _productionQualityDetailByNoEndpoint;
    private readonly string _productionQualityScanMaterialEndpoint;
    private readonly string _productionQualityCommitEndpoint;
    private readonly string _productionQualityFirstInspectionCommitEndpoint;
    private readonly string _productionQualityPicklingCommitEndpoint;
    private readonly string _productionQualitySamplingOrFullCommitEndpoint;
    private readonly string _productionQualitySamplingOrFullCompleteEndpoint;
    private readonly string _manualInspectionCreateEndpoint;
    private readonly string _manualInspectionDetailEndpoint;
    private readonly string _manualInspectionSaveResultEndpoint;
    private readonly string _manualInspectionCompleteEndpoint;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public QualityApi(HttpClient http, IConfigLoader configLoader)
    {
        _http = http;
        var servicePath = _http.BaseAddress?.AbsolutePath?.TrimEnd('/') ?? "/jxhljszpService";
        _incomingQualityDictListEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("incomingQualityOrder.getDictList", "/pda/qs/qsIncomingQualityOrder/getDictList"), servicePath);
        _incomingQualityListEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("incomingQualityOrder.list", "/pda/qs/qsIncomingQualityOrder/list"), servicePath);
        _incomingQualityDetailEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("incomingQualityOrder.detail", "/pda/qs/qsIncomingQualityOrder/detail"), servicePath);
        _incomingQualityScanEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("incomingQualityOrder.scan", "/pda/qs/qsIncomingQualityOrder/scan"), servicePath);
        _incomingQualitySaveResultEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("incomingQualityOrder.saveResult", "/pda/qs/qsIncomingQualityOrder/saveResult"), servicePath);
        _incomingQualityCompletedEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("incomingQualityOrder.completed", "/pda/qs/qsIncomingQualityOrder/completed"), servicePath);
        _incomingQualityDeleteEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("incomingQualityOrder.delete", "/pda/qs/qsIncomingQualityOrder/delete"), servicePath);
        _productionQualityListEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("productionQualityOrder.list", "/pda/qsOrderQuality/list"), servicePath);
        _productionQualityDetailByNoEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("productionQualityOrder.detailByNo", "/pda/qsOrderQuality/detailByNo"), servicePath);
        _productionQualityScanMaterialEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("productionQualityOrder.scanMaterial", "/pda/qsOrderQuality/scanMaterial"), servicePath);
        _productionQualityCommitEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("productionQualityOrder.commit", "/pda/qsOrderQuality/commit"), servicePath);
        _productionQualityFirstInspectionCommitEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("productionQualityOrder.firstInspectionCommit", "/pda/qsOrderQuality/firstInspectionCommit"), servicePath);
        _productionQualityPicklingCommitEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("productionQualityOrder.picklingCommit", "/pda/qsOrderQuality/picklingCommit"), servicePath);
        _productionQualitySamplingOrFullCommitEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("productionQualityOrder.samplingOrFullCommit", "/pda/qsOrderQuality/samplingOrFullCommit"), servicePath);
        _productionQualitySamplingOrFullCompleteEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("productionQualityOrder.samplingOrFullComplete", "/pda/qsOrderQuality/samplingOrFullComplete"), servicePath);
        _manualInspectionCreateEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("manualInspection.create", "/pda/manualInspection/create"), servicePath);
        _manualInspectionDetailEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("manualInspection.detail", "/pda/manualInspection/detail"), servicePath);
        _manualInspectionSaveResultEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("manualInspection.saveResult", "/pda/manualInspection/saveResult"), servicePath);
        _manualInspectionCompleteEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("manualInspection.complete", "/pda/manualInspection/complete"), servicePath);
    }

    public async Task<List<IncomingQualityStatusFilter>> GetIncomingQualityStatusFiltersAsync(CancellationToken ct = default)
    {
        var names = await LoadIncomingQualityStatusNamesAsync(ct).ConfigureAwait(false);
        var filters = new List<IncomingQualityStatusFilter> { new() { Name = "全部", Value = null, IsSelected = true } };
        filters.AddRange(names.Select(item => new IncomingQualityStatusFilter { Name = item.Value, Value = item.Key }));
        return filters;
    }

    public async Task<List<IncomingQualityOrderDto>> GetIncomingQualityOrdersAsync(string? status, CancellationToken ct = default)
    {
        var statusNames = await LoadIncomingQualityStatusNamesAsync(ct).ConfigureAwait(false);
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _incomingQualityListEndpoint);
        var request = new Dictionary<string, string>
        {
            [nameof(status)] = status ?? string.Empty
        };
        using var resp = await _http.PostAsJsonAsync(url, request, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<List<IncomingQualityOrderDto>>(resp, ct).ConfigureAwait(false);
        var list = data.result ?? new List<IncomingQualityOrderDto>();
        foreach (var item in list)
        {
            if (!string.IsNullOrWhiteSpace(item.status) && statusNames.TryGetValue(item.status, out var statusName))
            {
                item.statusName = statusName;
            }
            if (!string.IsNullOrWhiteSpace(item.delStatus) && statusNames.TryGetValue(item.delStatus, out var delStatusName))
            {
                item.delStatusName = delStatusName;
            }
        }
        return list;
    }

    public async Task<IncomingQualityOrderDetailDto> GetIncomingQualityOrderDetailAsync(string incomingQualityNo, CancellationToken ct = default)
    {
        var statusNames = await LoadIncomingQualityStatusNamesAsync(ct).ConfigureAwait(false);
        var inspectResultNames = await LoadDictOptionsAsync("inspectResult", ct).ConfigureAwait(false);
        var problemPointNames = await LoadDictOptionsAsync("problemPoint", ct).ConfigureAwait(false);
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _incomingQualityDetailEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, new { incomingQualityNo }, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<IncomingQualityOrderDetailDto>(resp, ct).ConfigureAwait(false);
        var detail = data.result ?? new IncomingQualityOrderDetailDto();
        if (!string.IsNullOrWhiteSpace(detail.status) && statusNames.TryGetValue(detail.status, out var statusName))
        {
            detail.statusName = statusName;
        }
        if (!string.IsNullOrWhiteSpace(detail.delStatus) && statusNames.TryGetValue(detail.delStatus, out var name))
        {
            detail.delStatusName = name;
        }
        foreach (var item in detail.scanDetails)
        {
            if (!string.IsNullOrWhiteSpace(item.inspectResult))
            {
                item.inspectResultName = inspectResultNames.FirstOrDefault(option => option.Value == item.inspectResult)?.Name;
            }
            if (!string.IsNullOrWhiteSpace(item.problemPoint))
            {
                item.problemPointName = ResolveDictNames(item.problemPoint, problemPointNames);
            }
        }
        return detail;
    }


    private static string? ResolveDictNames(string value, IReadOnlyCollection<QualityDictOption> options)
    {
        var names = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => options.FirstOrDefault(option => option.Value == part)?.Name ?? part)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        return names.Count == 0 ? null : string.Join("、", names);
    }

    public async Task<IncomingQualityScanMaterialDto> ScanIncomingQualityMaterialAsync(string qrCode, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _incomingQualityScanEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, new { qrCode }, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<IncomingQualityScanMaterialDto>(resp, ct).ConfigureAwait(false);
        return data.result ?? new IncomingQualityScanMaterialDto { qrCode = qrCode };
    }

    public async Task<List<QualityDictOption>> GetInspectResultOptionsAsync(CancellationToken ct = default)
    {
        return await LoadDictOptionsAsync("inspectResult", ct).ConfigureAwait(false);
    }

    public async Task<List<QualityDictOption>> GetProblemPointOptionsAsync(CancellationToken ct = default)
    {
        return await LoadDictOptionsAsync("problemPoint", ct).ConfigureAwait(false);
    }

    public async Task<bool> SaveIncomingQualityResultAsync(IncomingQualitySaveResultRequestDto request, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _incomingQualitySaveResultEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, request, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<bool?>(resp, ct).ConfigureAwait(false);
        return BooleanResultOrFalse(data);
    }

    public async Task<bool> CompleteIncomingQualityOrderAsync(string incomingQualityNo, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _incomingQualityCompletedEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, new { incomingQualityNo }, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<int?>(resp, ct).ConfigureAwait(false);
        return (data.result ?? 0) > 0 || data.success != false;
    }

    public async Task<bool> DeleteIncomingQualityOrderAsync(string incomingQualityNo, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _incomingQualityDeleteEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, new { incomingQualityNo }, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<bool?>(resp, ct).ConfigureAwait(false);
        return BooleanResultOrFalse(data);
    }

    public Task<List<ProductionQualityOrderDto>> GetProductionQualityOrdersByResourceAsync(string resourceCode, CancellationToken ct = default)
    {
        return QueryProductionQualityOrdersAsync(resourceName: null, inspectStatus: null, resourceCode: resourceCode, qualityNo: null, ct: ct);
    }

    public Task<List<ProductionQualityOrderDto>> GetProductionQualityOrdersAsync(string? resourceName, string? inspectStatus, string? qualityNo = null, CancellationToken ct = default)
    {
        return QueryProductionQualityOrdersAsync(resourceName: resourceName, inspectStatus: inspectStatus, resourceCode: null, qualityNo: qualityNo, ct: ct);
    }

    private async Task<List<ProductionQualityOrderDto>> QueryProductionQualityOrdersAsync(string? resourceName, string? inspectStatus, string? resourceCode, string? qualityNo, CancellationToken ct)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _productionQualityListEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, new
        {
            createdTimeBegin = string.Empty,
            createdTimeEnd = string.Empty,
            inspectStatus = inspectStatus ?? string.Empty,
            inspectionSchemeCode = string.Empty,
            qualityNo = qualityNo ?? string.Empty,
            resourceCode = resourceCode ?? string.Empty,
            resourceName = resourceName ?? string.Empty
        }, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<List<ProductionQualityOrderDto>>(resp, ct).ConfigureAwait(false);
        return data.result ?? new List<ProductionQualityOrderDto>();
    }

    public async Task<ProductionQualityDetailDto> GetProductionQualityDetailAsync(string qualityNo, string workOrderNo, CancellationToken ct = default)
    {
        var detail = await PostManualInspectionDetailAsync(qualityNo, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(detail.workOrderNo)) detail.workOrderNo = workOrderNo;
        return detail;
    }

    public async Task<ProductionQualityDetailDto> CreateManualInspectionAsync(string qrCode, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _manualInspectionCreateEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, new { qrCode }, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<ProductionQualityDetailDto>(resp, ct).ConfigureAwait(false);
        return data.result ?? new ProductionQualityDetailDto { qrCode = qrCode };
    }

    public Task<ProductionQualityDetailDto> GetManualInspectionDetailAsync(string qualityNo, CancellationToken ct = default)
    {
        return PostManualInspectionDetailAsync(qualityNo, ct);
    }

    private async Task<ProductionQualityDetailDto> PostManualInspectionDetailAsync(string qualityNo, CancellationToken ct)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _manualInspectionDetailEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, new { qualityNo }, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<ProductionQualityDetailDto>(resp, ct).ConfigureAwait(false);
        return data.result ?? new ProductionQualityDetailDto { qualityNo = qualityNo };
    }

    public async Task<bool> SaveManualInspectionResultAsync(ProductionManualInspectionSaveResultRequestDto request, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _manualInspectionSaveResultEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, request, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<bool?>(resp, ct).ConfigureAwait(false);
        return BooleanResultOrFalse(data);
    }

    public async Task<ProductionQualityDetailDto> CompleteManualInspectionAsync(string qualityNo, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _manualInspectionCompleteEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, new { qualityNo }, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<ProductionQualityDetailDto>(resp, ct).ConfigureAwait(false);
        return data.result ?? new ProductionQualityDetailDto { qualityNo = qualityNo };
    }

    public async Task<ProductionQualityScanMaterialDto> ScanProductionQualityMaterialAsync(ProductionQualityScanMaterialRequestDto request, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _productionQualityScanMaterialEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, request, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<ProductionQualityScanMaterialDto>(resp, ct).ConfigureAwait(false);
        return data.result ?? new ProductionQualityScanMaterialDto { qrCode = request.qrCode };
    }

    public async Task<bool> CommitProductionQualityAsync(ProductionQualityCommitRequestDto request, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _productionQualityCommitEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, request, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<bool?>(resp, ct).ConfigureAwait(false);
        return BooleanResultOrFalse(data);
    }

    public async Task<bool> CommitProductionFirstInspectionAsync(ProductionFirstInspectionCommitRequestDto request, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _productionQualityFirstInspectionCommitEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, request, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<bool?>(resp, ct).ConfigureAwait(false);
        return BooleanResultOrFalse(data);
    }

    public async Task<bool> CommitProductionPicklingAsync(ProductionPicklingCommitRequestDto request, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _productionQualityPicklingCommitEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, request, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<bool?>(resp, ct).ConfigureAwait(false);
        return BooleanResultOrFalse(data);
    }

    public async Task<bool> CommitProductionSamplingOrFullAsync(ProductionSamplingOrFullCommitRequestDto request, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _productionQualitySamplingOrFullCommitEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, request, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<bool?>(resp, ct).ConfigureAwait(false);
        return BooleanResultOrFalse(data);
    }

    public async Task<bool> CompleteProductionSamplingOrFullAsync(ProductionSamplingOrFullCompleteRequestDto request, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _productionQualitySamplingOrFullCompleteEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, request, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<bool?>(resp, ct).ConfigureAwait(false);
        return BooleanResultOrFalse(data);
    }

    private static bool BooleanResultOrFalse(ApiResp<bool?> data)
    {
        return data.result == true;
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadIncomingQualityStatusNamesAsync(CancellationToken ct)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _incomingQualityDictListEndpoint);
        using var resp = await _http.PostAsync(url, new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()), ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<List<DictGroupDto>>(resp, ct).ConfigureAwait(false);
        return data.result?
            .FirstOrDefault(group => string.Equals(group.field, "status", StringComparison.OrdinalIgnoreCase)
                || string.Equals(group.field, "delStatus", StringComparison.OrdinalIgnoreCase))?
            .dictItems?
            .Where(item => !string.IsNullOrWhiteSpace(item.dictItemValue) && !string.IsNullOrWhiteSpace(item.dictItemName))
            .GroupBy(item => item.dictItemValue!)
            .ToDictionary(group => group.Key, group => group.First().dictItemName!)
            ?? new Dictionary<string, string>();
    }

    private async Task<List<QualityDictOption>> LoadDictOptionsAsync(string field, CancellationToken ct)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _incomingQualityDictListEndpoint);
        using var resp = await _http.PostAsync(url, new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()), ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<List<DictGroupDto>>(resp, ct).ConfigureAwait(false);
        return data.result?
            .FirstOrDefault(group => string.Equals(group.field, field, StringComparison.OrdinalIgnoreCase))?
            .dictItems?
            .Where(item => !string.IsNullOrWhiteSpace(item.dictItemValue) && !string.IsNullOrWhiteSpace(item.dictItemName))
            .Select(item => new QualityDictOption { Value = item.dictItemValue!, Name = item.dictItemName! })
            .ToList()
            ?? new List<QualityDictOption>();
    }

    private static async Task<ApiResp<T>> ReadApiResponseAsync<T>(HttpResponseMessage resp, CancellationToken ct)
    {
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<T>>(stream, JsonOptions, ct).ConfigureAwait(false);
        if (data is null) throw new InvalidOperationException("接口返回为空。");
        if (data.success == false) throw new InvalidOperationException(string.IsNullOrWhiteSpace(data.message) ? "接口返回失败。" : data.message);
        return data;
    }
}
