using IndustrialControlMAUI.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace IndustrialControlMAUI.Services
{
    // ===================== 接口定义 =====================
    public interface IEquipmentApi
    {
        Task<PageResponeResult<InspectionRecordDto>> PageQueryAsync(
         int pageNo,
         int pageSize,
         string? inspectNo,
         string? createdTimeBegin,
         string? createdTimeEnd,
         string? inspectStatus,
         bool searchCount,
         CancellationToken ct = default);
        Task<DictInspection> GetInspectionDictsAsync(CancellationToken ct = default);
        Task<ApiResp<InspectionDetailDto>?> GetDetailAsync(string id, CancellationToken ct = default);
        Task<ApiResp<bool>> ExecuteSaveAsync(InspectionDetailDto payload, CancellationToken ct = default);
        Task<ApiResp<bool>> ExecuteCompleteInspectionAsync(InspectionDetailDto payload, CancellationToken ct = default);
        Task<ApiResp<DefectPage>> GetDefectPageAsync(
    int pageNo, int pageSize,
    string? defectCode = null,
    string? defectName = null,
    string? levelCode = null,
    string? status = null,
    bool? searchCount = null,
    string? createdTimeBegin = null,
    string? createdTimeEnd = null,
    CancellationToken ct = default);

        Task<ApiResp<bool>> DeleteAttachmentAsync(string id, CancellationToken ct = default);

        Task<ApiResp<List<InspectWorkflowNode>>> GetWorkflowAsync(string id, CancellationToken ct = default);

    }


    // ===================== 实现 =====================
    public class EquipmentApi : IEquipmentApi
    {
        private readonly HttpClient _http;
        private readonly string _pageEndpoint;
        private readonly string _dictEndpoint;
        private readonly string _detailsEndpoint;
        private readonly string _executeSavePath;
        private readonly string _executeCompletePath;
        private readonly string _defectPagePath;
        private readonly string _deleteAttPath;
        private readonly string _workflowPath;


        private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

        public EquipmentApi(HttpClient http, IConfigLoader configLoader)
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
                configLoader.GetApiPath("quality.page", "/pda/dev/inspectTask/pageQuery"), servicePath);
            _dictEndpoint = NormalizeRelative(
               configLoader.GetApiPath("quality.dictList", "/pda/dev/inspectTask/getDictList"), servicePath);
            _detailsEndpoint = NormalizeRelative(
               configLoader.GetApiPath("quality.detailList", "/pda/dev/inspectTask/detail"), servicePath);
            _executeSavePath = NormalizeRelative(
             configLoader.GetApiPath("quality.executeSave", "/pda/dev/inspectTask/executeSave"), servicePath);
            _executeCompletePath = NormalizeRelative(
                configLoader.GetApiPath("quality.executeComplete", "/pda/dev/inspectTask/executeCompleteInspection"), servicePath);
            _deleteAttPath = NormalizeRelative(
                configLoader.GetApiPath("quality.deleteAttPath", "/pda/dev/inspectTask/deleteAttachment"),
                servicePath);
            _workflowPath = NormalizeRelative(
    configLoader.GetApiPath("quality.workflow", "/pda/dev/inspectTask/getWorkflow"),
    servicePath);

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
        public async Task<PageResponeResult<InspectionRecordDto>> PageQueryAsync(
             int pageNo,
             int pageSize,
             string? inspectNo,
             string? createdTimeBegin,
             string? createdTimeEnd,
             string? inspectStatus,
             bool searchCount,
             CancellationToken ct = default)
        {
            // 1) 组装查询参数（仅在有值时加入）
            var p = new Dictionary<string, string>
            {
                ["pageNo"] = pageNo.ToString(),
                ["pageSize"] = pageSize.ToString(),
                ["searchCount"] = searchCount ? "true" : "false"
            };
            if (!string.IsNullOrWhiteSpace(inspectNo)) p["qualityNo"] = inspectNo!.Trim();
            if (!string.IsNullOrWhiteSpace(createdTimeBegin)) p["createdTimeBegin"] = createdTimeBegin!;
            if (!string.IsNullOrWhiteSpace(createdTimeEnd)) p["createdTimeEnd"] = createdTimeEnd!;
            if (!string.IsNullOrWhiteSpace(inspectStatus)) p["inspectStatus"] = inspectStatus!;

            // 2) 拼接 URL（与现有工具方法保持一致）
            var url = _pageEndpoint + "?" + BuildQuery(p);                  // BuildQuery 会做 UrlEncode
            var full = BuildFullUrl(_http.BaseAddress, url);

            // 3) 发送 GET
            using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(full, UriKind.Absolute));
            using var httpResp = await _http.SendAsync(req, ct);
            var json = await httpResp.Content.ReadAsStringAsync(ct);

            // 4) 非 2xx -> 返回一个失败的包装
            if (!httpResp.IsSuccessStatusCode)
            {
                return new PageResponeResult<InspectionRecordDto>
                {
                    success = false,
                    code = (int)httpResp.StatusCode,
                    message = $"HTTP {(int)httpResp.StatusCode}"
                };
            }

            // 5) 反序列化（大小写不敏感）
            return JsonSerializer.Deserialize<PageResponeResult<InspectionRecordDto>>(
                       json,
                       new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                   ) ?? new PageResponeResult<InspectionRecordDto> { success = false, message = "Empty body" };
        }


        public async Task<DictInspection> GetInspectionDictsAsync(CancellationToken ct = default)
        {
            var full = BuildFullUrl(_http.BaseAddress, _dictEndpoint);

            using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(full, UriKind.Absolute));
            using var res = await _http.SendAsync(req, ct);
            var json = await res.Content.ReadAsStringAsync(ct);

            var dto = JsonSerializer.Deserialize<DictResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var all = dto?.result ?? new List<DictField>();
            var inspectStatus = all.FirstOrDefault(f =>
       string.Equals(f.field, "inspectStatus", StringComparison.OrdinalIgnoreCase))
       ?.dictItems ?? new List<DictItem>();

            return new DictInspection { InspectStatus = inspectStatus };
        }
        // Services/InspectionApi.cs 追加方法（沿用你 BuildQuery/BuildFullUrl 风格）
        public async Task<ApiResp<InspectionDetailDto>?> GetDetailAsync(string id, CancellationToken ct = default)
        {
            var url = _detailsEndpoint + "?id=" + Uri.EscapeDataString(id ?? "");
            var full = BuildFullUrl(_http.BaseAddress, url);
            using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(full, UriKind.Absolute));
            using var res = await _http.SendAsync(req, ct);
            var json = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
            {
                return new ApiResp<InspectionDetailDto>
                {
                    success = false,
                    code = (int)res.StatusCode,
                    message = $"HTTP {(int)res.StatusCode}"
                };
            }

            return JsonSerializer.Deserialize<ApiResp<InspectionDetailDto>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            ) ?? new ApiResp<InspectionDetailDto> { success = false, message = "Empty body" };
        }




        public async Task<ApiResp<bool>> ExecuteSaveAsync(InspectionDetailDto payload, CancellationToken ct = default)
        {
            var url = BuildFullUrl(_http.BaseAddress, _executeSavePath);
            var json = JsonSerializer.Serialize(payload, _json);
            using var req = new HttpRequestMessage(HttpMethod.Post, new Uri(url, UriKind.Absolute))
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
            using var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
                return new ApiResp<bool> { success = false, code = (int)res.StatusCode, message = $"HTTP {(int)res.StatusCode}" };

            return JsonSerializer.Deserialize<ApiResp<bool>>(body, _json)
                   ?? new ApiResp<bool> { success = false, message = "Empty body" };
        }

        public async Task<ApiResp<bool>> ExecuteCompleteInspectionAsync(InspectionDetailDto payload, CancellationToken ct = default)
        {
            var url = BuildFullUrl(_http.BaseAddress, _executeCompletePath);
            var json = JsonSerializer.Serialize(payload, _json);
            using var req = new HttpRequestMessage(HttpMethod.Post, new Uri(url, UriKind.Absolute))
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
            using var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
                return new ApiResp<bool> { success = false, code = (int)res.StatusCode, message = $"HTTP {(int)res.StatusCode}" };

            return JsonSerializer.Deserialize<ApiResp<bool>>(body, _json)
                   ?? new ApiResp<bool> { success = false, message = "Empty body" };
        }
        public async Task<ApiResp<DefectPage>> GetDefectPageAsync(
                 int pageNo, int pageSize,
                 string? defectCode = null,
                 string? defectName = null,
                 string? levelCode = null,
                 string? status = null,
                 bool? searchCount = null,
                 string? createdTimeBegin = null,
                 string? createdTimeEnd = null,
                 CancellationToken ct = default)
        {
            var url = BuildFullUrl(_http.BaseAddress, _defectPagePath);

            // 该接口是 GET + query（表单编码），必填 pageNo/pageSize
            var q = new List<string>
    {
        $"pageNo={pageNo}",
        $"pageSize={pageSize}"
    };
            if (!string.IsNullOrWhiteSpace(defectCode)) q.Add($"defectCode={Uri.EscapeDataString(defectCode)}");
            if (!string.IsNullOrWhiteSpace(defectName)) q.Add($"defectName={Uri.EscapeDataString(defectName)}");
            if (!string.IsNullOrWhiteSpace(levelCode)) q.Add($"levelCode={Uri.EscapeDataString(levelCode)}");
            if (!string.IsNullOrWhiteSpace(status)) q.Add($"status={Uri.EscapeDataString(status)}");
            if (searchCount.HasValue) q.Add($"searchCount={(searchCount.Value ? "true" : "false")}");
            if (!string.IsNullOrWhiteSpace(createdTimeBegin)) q.Add($"createdTimeBegin={Uri.EscapeDataString(createdTimeBegin)}");
            if (!string.IsNullOrWhiteSpace(createdTimeEnd)) q.Add($"createdTimeEnd={Uri.EscapeDataString(createdTimeEnd)}");

            var full = url + "?" + string.Join("&", q);
            using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(full, UriKind.Absolute));
            using var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
                return new ApiResp<DefectPage> { success = false, code = (int)res.StatusCode, message = $"HTTP {(int)res.StatusCode}" };

            return System.Text.Json.JsonSerializer.Deserialize<ApiResp<DefectPage>>(body, _json)
                   ?? new ApiResp<DefectPage> { success = false, message = "Empty body" };
        }

        public async Task<ApiResp<bool>> DeleteAttachmentAsync(string id, CancellationToken ct = default)
        {
            var url = BuildFullUrl(_http.BaseAddress, _deleteAttPath);
            var reqObj = new DeleteAttachmentReq { id = id };
            var json = JsonSerializer.Serialize(reqObj, _json);
            using var req = new HttpRequestMessage(HttpMethod.Post, new Uri(url, UriKind.Absolute))
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
            using var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
                return new ApiResp<bool> { success = false, code = (int)res.StatusCode, message = $"HTTP {(int)res.StatusCode}" };

            return JsonSerializer.Deserialize<ApiResp<bool>>(body, _json)
                   ?? new ApiResp<bool> { success = false, message = "Empty body" };
        }

        public async Task<ApiResp<List<InspectWorkflowNode>>> GetWorkflowAsync(string id, CancellationToken ct = default)
        {
            var url = BuildFullUrl(_http.BaseAddress, _workflowPath) + "?id=" + Uri.EscapeDataString(id ?? "");
            using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(url, UriKind.Absolute));
            using var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
                return new ApiResp<List<InspectWorkflowNode>>
                {
                    success = false,
                    code = (int)res.StatusCode,
                    message = $"HTTP {(int)res.StatusCode}"
                };

            return System.Text.Json.JsonSerializer.Deserialize<ApiResp<List<InspectWorkflowNode>>>(
                       body,
                       new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                   ) ?? new ApiResp<List<InspectWorkflowNode>> { success = false, message = "Empty body" };
        }



    }
}