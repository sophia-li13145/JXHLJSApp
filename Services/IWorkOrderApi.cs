using System.Net.Http.Headers;
using System.Text.Json;

namespace IndustrialControlMAUI.Services
{
    // ===================== 接口定义 =====================
    public interface IWorkOrderApi
    {
        Task<WorkOrderPageResult> GetWorkOrdersAsync(WorkOrderQuery q, CancellationToken ct = default);
        Task<DictBundle> GetWorkOrderDictsAsync(CancellationToken ct = default);
        Task<WorkflowResp?> GetWorkOrderWorkflowAsync(string id, CancellationToken ct = default);
        Task<PageResp<ProcessTask>?> PageWorkProcessTasksAsync(string workOrderNo, int pageNo = 1, int pageSize = 50, CancellationToken ct = default);
    }

    // ===================== 实现 =====================
    public class WorkOrderApi : IWorkOrderApi
    {
        private readonly HttpClient _http;
        private readonly string _pageEndpoint;
        private readonly string _workflowEndpoint;
        private readonly string _processTasksEndpoint;
        private readonly string _dictEndpoint;

        public WorkOrderApi(HttpClient http, IConfigLoader configLoader)
        {
            _http = http;
            if (_http.Timeout == System.Threading.Timeout.InfiniteTimeSpan)
                _http.Timeout = TimeSpan.FromSeconds(15);

            var baseUrl = configLoader.GetBaseUrl();
            if (_http.BaseAddress is null)
                _http.BaseAddress = new Uri(baseUrl, UriKind.Absolute);

            var servicePath = _http.BaseAddress.AbsolutePath?.TrimEnd('/') ?? "/normalService";

            _http.DefaultRequestHeaders.Accept.Clear();
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            _pageEndpoint = NormalizeRelative(
                configLoader.GetApiPath("workOrder.page", "/pda/pmsWorkOrder/pageWorkOrders"), servicePath);
            _workflowEndpoint = NormalizeRelative(
                configLoader.GetApiPath("workOrder.workflow", "/pda/pmsWorkOrder/getWorkOrderWorkflow"), servicePath);
            _processTasksEndpoint = NormalizeRelative(
                configLoader.GetApiPath("workOrder.processTasks", "/pda/pmsWorkOrder/pageWorkProcessTasks"), servicePath);
            _dictEndpoint = NormalizeRelative(
                configLoader.GetApiPath("workOrder.dictList", "/pda/pmsWorkOrder/getWorkOrderDictList"), servicePath);
        }

        // ===== 公共工具 =====
        private static string BuildFullUrl(Uri? baseAddress, string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("url 不能为空", nameof(url));

            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return url;

            if (baseAddress is null)
                throw new InvalidOperationException("HttpClient.BaseAddress 未配置");

            var baseUrl = baseAddress.AbsoluteUri;
            if (!baseUrl.EndsWith("/")) baseUrl += "/";

            return baseUrl + url.TrimStart('/');
        }

        private static string BuildQuery(IDictionary<string, string> p)
            => string.Join("&", p.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        private static string NormalizeRelative(string? endpoint, string servicePath)
        {
            var ep = (endpoint ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(ep)) return "/";

            if (ep.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                ep.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return ep;

            if (string.IsNullOrWhiteSpace(servicePath)) servicePath = "/";
            if (!servicePath.StartsWith("/")) servicePath = "/" + servicePath;
            servicePath = servicePath.TrimEnd('/');

            if (!string.IsNullOrEmpty(servicePath) &&
                servicePath != "/" &&
                ep.StartsWith(servicePath + "/", StringComparison.OrdinalIgnoreCase))
            {
                ep = ep[servicePath.Length..];
            }

            if (!ep.StartsWith("/")) ep = "/" + ep;
            return ep;
        }

        // ===== 方法实现 =====
        public async Task<WorkOrderPageResult> GetWorkOrdersAsync(WorkOrderQuery q, CancellationToken ct = default)
        {
            var p = new Dictionary<string, string>
            {
                ["pageNo"] = q.PageNo.ToString(),
                ["pageSize"] = q.PageSize.ToString()
            };
            if (q.CreatedTimeStart.HasValue) p["createdTimeStart"] = q.CreatedTimeStart.Value.ToString("yyyy-MM-dd HH:mm:ss");
            if (q.CreatedTimeEnd.HasValue) p["createdTimeEnd"] = q.CreatedTimeEnd.Value.ToString("yyyy-MM-dd HH:mm:ss");
            if (!string.IsNullOrWhiteSpace(q.WorkOrderNo)) p["workOrderNo"] = q.WorkOrderNo!.Trim();
            if (!string.IsNullOrWhiteSpace(q.MaterialName)) p["materialName"] = q.MaterialName!.Trim();

            var url = _pageEndpoint + "?" + BuildQuery(p);
            var full = BuildFullUrl(_http.BaseAddress, url);

            using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(full, UriKind.Absolute));
            using var httpResp = await _http.SendAsync(req, ct);
            var json = await httpResp.Content.ReadAsStringAsync(ct);

            if (!httpResp.IsSuccessStatusCode)
                return new WorkOrderPageResult { success = false, message = $"HTTP {(int)httpResp.StatusCode}" };

            return JsonSerializer.Deserialize<WorkOrderPageResult>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new WorkOrderPageResult();
        }

        public async Task<WorkflowResp?> GetWorkOrderWorkflowAsync(string id, CancellationToken ct = default)
        {
            var url = _workflowEndpoint + "?id=" + Uri.EscapeDataString(id ?? "");
            var full = BuildFullUrl(_http.BaseAddress, url);

            using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(full, UriKind.Absolute));
            using var httpResp = await _http.SendAsync(req, ct);
            var json = await httpResp.Content.ReadAsStringAsync(ct);

            if (!httpResp.IsSuccessStatusCode)
                return new WorkflowResp { success = false, message = $"HTTP {(int)httpResp.StatusCode}" };

            return JsonSerializer.Deserialize<WorkflowResp>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new WorkflowResp();
        }

        public async Task<PageResp<ProcessTask>?> PageWorkProcessTasksAsync(string workOrderNo, int pageNo = 1, int pageSize = 50, CancellationToken ct = default)
        {
            var p = new Dictionary<string, string>
            {
                ["pageNo"] = pageNo.ToString(),
                ["pageSize"] = pageSize.ToString()
            };
            if (!string.IsNullOrWhiteSpace(workOrderNo)) p["workOrderNo"] = workOrderNo.Trim();

            var url = _processTasksEndpoint + "?" + BuildQuery(p);
            var full = BuildFullUrl(_http.BaseAddress, url);

            using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(full, UriKind.Absolute));
            using var httpResp = await _http.SendAsync(req, ct);
            var json = await httpResp.Content.ReadAsStringAsync(ct);

            if (!httpResp.IsSuccessStatusCode)
                return new PageResp<ProcessTask> { success = false, message = $"HTTP {(int)httpResp.StatusCode}" };

            return JsonSerializer.Deserialize<PageResp<ProcessTask>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new PageResp<ProcessTask>();
        }

        public async Task<DictBundle> GetWorkOrderDictsAsync(CancellationToken ct = default)
        {
            var full = BuildFullUrl(_http.BaseAddress, _dictEndpoint);

            using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(full, UriKind.Absolute));
            using var res = await _http.SendAsync(req, ct);
            var json = await res.Content.ReadAsStringAsync(ct);

            var dto = JsonSerializer.Deserialize<DictResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var all = dto?.result ?? new List<DictField>();
            var audit = all.FirstOrDefault(f => string.Equals(f.field, "auditStatus", StringComparison.OrdinalIgnoreCase))
                          ?.dictItems ?? new List<DictItem>();
            var urgent = all.FirstOrDefault(f => string.Equals(f.field, "urgent", StringComparison.OrdinalIgnoreCase))
                          ?.dictItems ?? new List<DictItem>();

            return new DictBundle { AuditStatus = audit, Urgent = urgent };
        }
    }

    // ===================== 模型（与之前一致，可放在 Models 下） =====================
    public class WorkOrderQuery
    {
        public int PageNo { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public string? AuditStatus { get; set; }
        public DateTime? CreatedTimeStart { get; set; }
        public DateTime? CreatedTimeEnd { get; set; }
        public string? WorkOrderNo { get; set; }
        public string? MaterialName { get; set; }
    }

    public class WorkOrderPageResult
    {
        public int code { get; set; }
        public string? message { get; set; }
        public bool success { get; set; }
        public WorkOrderPageData? result { get; set; }
        public long costTime { get; set; }
    }

    public class WorkOrderPageData
    {
        public WorkOrderPageList? list { get; set; }
        public int pageNo { get; set; }
        public int pageSize { get; set; }
        public long total { get; set; }
        public List<WorkOrderRecord> records { get; set; } = new();
    }

    public class WorkOrderPageList
    {
        public int pageNo { get; set; }
        public int pageSize { get; set; }
        public long total { get; set; }
        public List<WorkOrderRecord> records { get; set; } = new();
    }

    public class WorkOrderRecord
    {
        public string? id { get; set; }
        public string? workOrderNo { get; set; }
        public string? workOrderName { get; set; }
        public string? auditStatus { get; set; }
        public decimal? curQty { get; set; }
        public string? materialCode { get; set; }
        public string? materialName { get; set; }
        public string? line { get; set; }
        public string? lineName { get; set; }
        public string? workShop { get; set; }
        public string? workShopName { get; set; }
        public string? urgent { get; set; }
        public string? schemeStartDate { get; set; }
        public string? schemeEndDate { get; set; }
        public string? createdTime { get; set; }
        public string? modifiedTime { get; set; }
        public string? commitedTime { get; set; }
        public string? bomCode { get; set; }
        public string? routeName { get; set; }
    }

    public class DictResponse
    {
        public bool success { get; set; }
        public string? message { get; set; }
        public int code { get; set; }
        public List<DictField>? result { get; set; }
        public long costTime { get; set; }
    }

    public class DictField
    {
        public string? field { get; set; }
        public List<DictItem> dictItems { get; set; } = new();
    }

    public class DictItem
    {
        public string? dictItemValue { get; set; }
        public string? dictItemName { get; set; }
    }

    public class DictBundle
    {
        public List<DictItem> AuditStatus { get; set; } = new();
        public List<DictItem> Urgent { get; set; } = new();
    }

    public sealed class WorkflowResp
    {
        public bool success { get; set; }
        public string? message { get; set; }
        public int code { get; set; }
        public List<WorkflowItem>? result { get; set; }
    }

    public sealed class WorkflowItem
    {
        public string? statusValue { get; set; }
        public string? statusName { get; set; }
        public string? statusTime { get; set; }
    }

    public sealed class PageResp<T>
    {
        public bool success { get; set; }
        public string? message { get; set; }
        public int code { get; set; }
        public PageResult<T>? result { get; set; }
    }

    public sealed class PageResult<T>
    {
        public int pageNo { get; set; }
        public int pageSize { get; set; }
        public int total { get; set; }
        public List<T>? records { get; set; }
    }

    public sealed class ProcessTask
    {
        public string? id { get; set; }
        public string? processCode { get; set; }
        public string? processName { get; set; }
        public decimal? scheQty { get; set; }
        public decimal? completedQty { get; set; }
        public string? startDate { get; set; }
        public string? endDate { get; set; }
        public int? sortNumber { get; set; }
        public string? auditStatus { get; set; }
    }
}
