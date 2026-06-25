using JXHLJSApp.Models;
using JXHLJSApp.Models.WorkOrders;
using JXHLJSApp.Services.Common;
using System.Net.Http.Json;
using System.Text.Json;

namespace JXHLJSApp.Services.WorkOrders;

public interface IWorkOrderApi
{
    Task<List<WorkOrderTaskDto>> GetWorkOrderListAsync(string? deviceCode = null, string? machineNo = null, string? workOrderStatus = null, CancellationToken ct = default);
}

public sealed class WorkOrderApi : IWorkOrderApi
{
    private readonly HttpClient _http;
    private readonly string _listEndpoint;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public WorkOrderApi(HttpClient http, IConfigLoader configLoader)
    {
        _http = http;
        var servicePath = _http.BaseAddress?.AbsolutePath?.TrimEnd('/') ?? "/jxhljszpService";
        _listEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("workOrder.list", "/pda/pmsWorkOrder/getWorkOrderList"), servicePath);
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
        return data?.result ?? new List<WorkOrderTaskDto>();
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
