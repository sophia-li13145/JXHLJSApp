using System.Net.Http.Json;
using System.Text.Json;
using JXHLJSApp.Models;
using JXHLJSApp.Models.Warehouse;
using JXHLJSApp.Models.WorkOrders;
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
    private readonly string _workOrderDictEndpoint;
    private readonly string _productionQualityDictEndpoint;
    private readonly string _incomingQualityDictEndpoint;
    private IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? _productionDictNames;
    private IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? _incomingQualityDictNames;

    public MaterialScanQueryApi(HttpClient http, IConfigLoader configLoader)
    {
        _http = http;
        var servicePath = http.BaseAddress?.AbsolutePath?.TrimEnd('/') ?? "/jxhljszpService";
        _endpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("materialScanQuery.completeInfo", "/pda/wmsMaterialQrCode/scanQueryCompleteMaterialInfo"),
            servicePath);
        _workOrderDictEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("workOrder.dictList", "/pda/pmsWorkOrder/getWorkOrderDictList"), servicePath);
        _productionQualityDictEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("workOrder.wdictList", "/pda/qsOrderQuality/getDictList"), servicePath);
        _incomingQualityDictEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("incomingQualityOrder.getDictList", "/pda/qs/qsIncomingQualityOrder/getDictList"), servicePath);
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

        await ApplyDictNamesAsync(data.result, ct).ConfigureAwait(false);
        return data.result;
    }

    private async Task ApplyDictNamesAsync(MaterialScanQueryDto result, CancellationToken ct)
    {
        var productionNames = await GetProductionDictNamesAsync(ct).ConfigureAwait(false);

        if (result.basicInfo is { } basic)
        {
            // 基本信息中的产地与生产指令卡/生产质检页面使用同一个生产字典。
            basic.origin = MapDictName(basic.origin, productionNames, "originPlace");
        }

        if (result.instructionCardInfo is { } instruction)
        {
            instruction.drawMode = MapDictName(instruction.drawMode, productionNames, "drawMode");
            instruction.rawOrQuench = MapDictName(instruction.rawOrQuench, productionNames, "rawOrQuench");
            instruction.saleMode = MapDictName(instruction.saleMode, productionNames, "saleMode");
            instruction.wireShape = MapDictName(instruction.wireShape, productionNames, "wireShape");
            instruction.wireTakeUpSpeed = MapDictName(instruction.wireTakeUpSpeed, productionNames, "wireTakeUpSpeed");
        }

        if (result.inspectionInfo is { } inspection)
        {
            var qualityNames = await GetIncomingQualityDictNamesAsync(ct).ConfigureAwait(false);
            inspection.inspectResult = MapDictName(inspection.inspectResult, qualityNames, "inspectResult");
            inspection.problemPoint = MapSeparatedDictNames(inspection.problemPoint, qualityNames, "problemPoint");
        }
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> GetProductionDictNamesAsync(CancellationToken ct)
    {
        if (_productionDictNames is not null) return _productionDictNames;

        var workOrderGroups = await LoadGetDictGroupsAsync(_workOrderDictEndpoint, ct).ConfigureAwait(false);
        var qualityGroups = await LoadGetDictGroupsAsync(_productionQualityDictEndpoint, ct).ConfigureAwait(false);
        _productionDictNames = BuildDictNames(workOrderGroups.Concat(qualityGroups));
        return _productionDictNames;
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> GetIncomingQualityDictNamesAsync(CancellationToken ct)
    {
        if (_incomingQualityDictNames is not null) return _incomingQualityDictNames;

        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _incomingQualityDictEndpoint);
        using var response = await _http.PostAsync(
            url,
            new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()),
            ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<List<DictGroupDto>>>(stream, JsonOptions, ct).ConfigureAwait(false);
        EnsureDictSuccess(data);
        _incomingQualityDictNames = BuildDictNames((data?.result ?? new List<DictGroupDto>()).Select(ToWorkOrderDict));
        return _incomingQualityDictNames;
    }

    private async Task<List<WorkOrderDictDto>> LoadGetDictGroupsAsync(string endpoint, CancellationToken ct)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, endpoint);
        using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<List<WorkOrderDictDto>>>(stream, JsonOptions, ct).ConfigureAwait(false);
        EnsureDictSuccess(data);
        return data?.result ?? new List<WorkOrderDictDto>();
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> BuildDictNames(IEnumerable<WorkOrderDictDto> groups) =>
        groups.Where(group => !string.IsNullOrWhiteSpace(group.field))
            .GroupBy(group => group.field!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<string, string>)group
                    .SelectMany(item => item.dictItems ?? new List<WorkOrderDictItemDto>())
                    .Where(item => !string.IsNullOrWhiteSpace(item.dictItemValue) && !string.IsNullOrWhiteSpace(item.dictItemName))
                    .GroupBy(item => item.dictItemValue!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(item => item.Key, item => item.First().dictItemName!, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

    private static WorkOrderDictDto ToWorkOrderDict(DictGroupDto group) => new()
    {
        field = group.field,
        dictItems = group.dictItems?.Select(item => new WorkOrderDictItemDto
        {
            dictItemName = item.dictItemName,
            dictItemValue = item.dictItemValue
        }).ToList()
    };

    private static void EnsureDictSuccess<T>(ApiResp<T>? data)
    {
        if (data is null || !data.success) throw new InvalidOperationException(data?.message ?? "字典接口返回失败");
    }

    private static string? MapDictName(
        string? value,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> names,
        string field)
    {
        if (string.IsNullOrWhiteSpace(value) || !names.TryGetValue(field, out var itemNames)) return value;
        return itemNames.TryGetValue(value, out var name) ? name : value;
    }

    private static string? MapSeparatedDictNames(
        string? value,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> names,
        string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        return string.Join("、", value.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => MapDictName(item, names, field)));
    }
}
