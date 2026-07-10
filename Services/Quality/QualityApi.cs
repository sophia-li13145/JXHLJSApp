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
    Task<bool> DeleteIncomingQualityOrderAsync(string incomingQualityNo, CancellationToken ct = default);
    Task<List<ProductionQualityOrderDto>> GetProductionQualityOrdersByResourceAsync(string resourceCode, CancellationToken ct = default);
    Task<List<ProductionQualityOrderDto>> GetProductionQualityOrdersAsync(string? resourceName, string? inspectStatus, CancellationToken ct = default);
    Task<ProductionQualityDetailDto> GetProductionQualityDetailAsync(string qualityNo, string workOrderNo, CancellationToken ct = default);
    Task<bool> CommitProductionQualityAsync(ProductionQualityCommitRequestDto request, CancellationToken ct = default);
}

public sealed class QualityApi : IQualityApi
{
    private readonly HttpClient _http;
    private readonly string _incomingQualityDictListEndpoint;
    private readonly string _incomingQualityListEndpoint;
    private readonly string _incomingQualityDetailEndpoint;
    private readonly string _incomingQualityScanEndpoint;
    private readonly string _incomingQualitySaveResultEndpoint;
    private readonly string _incomingQualityDeleteEndpoint;
    private readonly string _productionQualityListEndpoint;
    private readonly string _productionQualityDetailByNoEndpoint;
    private readonly string _productionQualityCommitEndpoint;
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
        _incomingQualityDeleteEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("incomingQualityOrder.delete", "/pda/qs/qsIncomingQualityOrder/delete"), servicePath);
        _productionQualityListEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("productionQualityOrder.list", "/pda/qsOrderQuality/list"), servicePath);
        _productionQualityDetailByNoEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("productionQualityOrder.detailByNo", "/pda/qsOrderQuality/detailByNo"), servicePath);
        _productionQualityCommitEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("productionQualityOrder.commit", "/pda/qsOrderQuality/commit"), servicePath);
    }

    public async Task<List<IncomingQualityStatusFilter>> GetIncomingQualityStatusFiltersAsync(CancellationToken ct = default)
    {
        var names = await LoadDelStatusNamesAsync(ct).ConfigureAwait(false);
        var filters = new List<IncomingQualityStatusFilter> { new() { Name = "全部", Value = null, IsSelected = true } };
        filters.AddRange(names.Select(item => new IncomingQualityStatusFilter { Name = item.Value, Value = item.Key }));
        return filters;
    }

    public async Task<List<IncomingQualityOrderDto>> GetIncomingQualityOrdersAsync(string? status, CancellationToken ct = default)
    {
        var statusNames = await LoadDelStatusNamesAsync(ct).ConfigureAwait(false);
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
            if (!string.IsNullOrWhiteSpace(item.delStatus) && statusNames.TryGetValue(item.delStatus, out var name))
            {
                item.delStatusName = name;
            }
        }
        return list;
    }

    public async Task<IncomingQualityOrderDetailDto> GetIncomingQualityOrderDetailAsync(string incomingQualityNo, CancellationToken ct = default)
    {
        var statusNames = await LoadDelStatusNamesAsync(ct).ConfigureAwait(false);
        var inspectResultNames = await LoadDictOptionsAsync("inspectResult", ct).ConfigureAwait(false);
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _incomingQualityDetailEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, new { incomingQualityNo }, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<IncomingQualityOrderDetailDto>(resp, ct).ConfigureAwait(false);
        var detail = data.result ?? new IncomingQualityOrderDetailDto();
        if (!string.IsNullOrWhiteSpace(detail.delStatus) && statusNames.TryGetValue(detail.delStatus, out var name))
        {
            detail.delStatusName = name;
        }
        foreach (var item in detail.detailList ?? new List<IncomingQualityScanDetailDto>())
        {
            if (!string.IsNullOrWhiteSpace(item.inspectResult))
            {
                item.inspectResultName = inspectResultNames.FirstOrDefault(option => option.Value == item.inspectResult)?.Name;
            }
        }
        return detail;
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
        return QueryProductionQualityOrdersAsync(resourceName: null, inspectStatus: null, resourceCode: resourceCode, ct: ct);
    }

    public Task<List<ProductionQualityOrderDto>> GetProductionQualityOrdersAsync(string? resourceName, string? inspectStatus, CancellationToken ct = default)
    {
        return QueryProductionQualityOrdersAsync(resourceName: resourceName, inspectStatus: inspectStatus, resourceCode: null, ct: ct);
    }

    private async Task<List<ProductionQualityOrderDto>> QueryProductionQualityOrdersAsync(string? resourceName, string? inspectStatus, string? resourceCode, CancellationToken ct)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _productionQualityListEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, new
        {
            createdTimeBegin = string.Empty,
            createdTimeEnd = string.Empty,
            inspectStatus = inspectStatus ?? string.Empty,
            inspectionSchemeCode = string.Empty,
            qualityNo = string.Empty,
            resourceCode = resourceCode ?? string.Empty,
            resourceName = resourceName ?? string.Empty
        }, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<List<ProductionQualityOrderDto>>(resp, ct).ConfigureAwait(false);
        return data.result ?? new List<ProductionQualityOrderDto>();
    }

    public async Task<ProductionQualityDetailDto> GetProductionQualityDetailAsync(string qualityNo, string workOrderNo, CancellationToken ct = default)
    {
        var endpoint = $"{_productionQualityDetailByNoEndpoint}?qualityNo={Uri.EscapeDataString(qualityNo)}&workOrderNo={Uri.EscapeDataString(workOrderNo)}";
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, endpoint);
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<ProductionQualityDetailDto>(resp, ct).ConfigureAwait(false);
        return data.result ?? new ProductionQualityDetailDto { workOrderNo = workOrderNo };
    }

    public async Task<bool> CommitProductionQualityAsync(ProductionQualityCommitRequestDto request, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _productionQualityCommitEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, request, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<bool?>(resp, ct).ConfigureAwait(false);
        return BooleanResultOrFalse(data);
    }

    private static bool BooleanResultOrFalse(ApiResp<bool?> data)
    {
        return data.result == true;
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadDelStatusNamesAsync(CancellationToken ct)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _incomingQualityDictListEndpoint);
        using var resp = await _http.PostAsync(url, new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()), ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await ReadApiResponseAsync<List<DictGroupDto>>(resp, ct).ConfigureAwait(false);
        return data.result?
            .FirstOrDefault(group => string.Equals(group.field, "delStatus", StringComparison.OrdinalIgnoreCase))?
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
