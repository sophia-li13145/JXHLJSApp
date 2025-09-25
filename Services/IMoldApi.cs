using IndustrialControlMAUI.Models;
using System.Collections.ObjectModel;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace IndustrialControlMAUI.Services
{
    // ===================== 接口定义 =====================
    public interface IMoldApi
    {
        // ① 工单分页列表
        Task<WorkOrderPageResult> GetMoldsAsync(MoldQuery q, CancellationToken ct = default);

        Task<WorkflowResp?> GetMoldWorkflowAsync(string id, CancellationToken ct = default);
        Task<PageResp<ProcessTask>?> PageWorkProcessTasksAsync(string workOrderNo, int pageNo = 1, int pageSize = 50, CancellationToken ct = default);

        Task<IReadOnlyList<InboundScannedRow>> GetInStockScanDetailAsync(string instockId, CancellationToken ct = default);
        /// <summary>扫描条码入库</summary>
        Task<SimpleOk> InStockByBarcodeAsync(string instockId, string barcode, CancellationToken ct = default);
        /// <summary>模具入库的扫描查询接口</summary>
        Task<MoldScanQueryResp?> InStockScanQueryAsync(string code, CancellationToken ct = default);
        /// <summary>PDA 扫描通过（确认当前入库单已扫描项）</summary>
        Task<SimpleOk> ConfirmInStockByListAsync(InStockConfirmReq req, CancellationToken ct = default);

        //-------------------------------出库-------------------------------------------
        /// <summary>
        /// 返回页面所需：工单号、物料名称、以及“型号+基础需求数量+模具编码列表”的分组视图
        /// </summary>
        Task<WorkOrderMoldView> GetViewAsync(string workOrderNo, CancellationToken ct = default);

        /// <summary>出库扫描查询</summary>
        Task<MoldOutScanQueryResp?> OutStockScanQueryAsync(string code, string workOrderNo, CancellationToken ct = default);

        /// <summary>确认出库</summary>
        Task<SimpleOk> ConfirmOutStockAsync(MoldOutConfirmReq req, CancellationToken ct = default);
    }

    // ===================== 实现 =====================
    public class MoldApi : IMoldApi
    {
        private readonly HttpClient _http;

        // 统一由 appconfig.json 管理的端点路径（相对服务路径）
        private readonly string _queryForWorkOrderEndpoint;
        private readonly string _pageEndpoint;
        private readonly string _workflowEndpoint;
        private readonly string _processTasksEndpoint;
        private readonly string _dictEndpoint;
        private readonly string _scanDetailEndpoint;
        private readonly string _scanByBarcodeEndpoint;

        private readonly string _scanQueryEndpoint;
        private readonly string _confirmInStockEndpoint;
        private readonly string _outStockScanQueryEndpoint;
        private readonly string _outStockConfirmEndpoint;

        public MoldApi(HttpClient http, IConfigLoader configLoader)
        {
            _http = http;

            if (_http.Timeout == System.Threading.Timeout.InfiniteTimeSpan)
                _http.Timeout = TimeSpan.FromSeconds(15);

            // === 基地址 ===
            var baseUrl = configLoader.GetBaseUrl();
            if (_http.BaseAddress is null)
                _http.BaseAddress = new Uri(baseUrl, UriKind.Absolute);

            // 服务路径（如 /normalService），用于去重
            var servicePath = _http.BaseAddress.AbsolutePath?.TrimEnd('/') ?? "/normalService";

            // 接受 JSON
            _http.DefaultRequestHeaders.Accept.Clear();
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // === Endpoints ===
            _pageEndpoint = NormalizeRelative(
                configLoader.GetApiPath("workOrder.page", "/pda/pmsMold/pageMolds"),
                servicePath);

            _workflowEndpoint = NormalizeRelative(
                configLoader.GetApiPath("workOrder.workflow", "/pda/pmsMold/getMoldWorkflow"),
                servicePath);

            _processTasksEndpoint = NormalizeRelative(
                configLoader.GetApiPath("workOrder.processTasks", "/pda/pmsMold/pageWorkProcessTasks"),
                servicePath);

            _dictEndpoint = NormalizeRelative(
                configLoader.GetApiPath("workOrder.dictList", "/pda/pmsMold/getMoldDictList"),
                servicePath);

            _scanDetailEndpoint = NormalizeRelative(
                configLoader.GetApiPath("mold.inStockScanDetail", "/pda/mold/getInStockScanDetail"),
                servicePath);

            _scanByBarcodeEndpoint = NormalizeRelative(
                configLoader.GetApiPath("mold.inStockScanByBarcode", "/pda/mold/scanByBarcode"),
                servicePath);

            _scanQueryEndpoint = NormalizeRelative(
                configLoader.GetApiPath("mold.inStockScanQuery", "/pda/mold/inStockScanQuery"),
                servicePath);

            _confirmInStockEndpoint = NormalizeRelative(
                configLoader.GetApiPath("mold.inStock", "/pda/mold/inStock"),
                servicePath);

            //-------------------------------出库-------------------------------------------
            _queryForWorkOrderEndpoint = NormalizeRelative(
                configLoader.GetApiPath("mold.queryForWorkOrder", "/pda/mold/queryForWorkOrder"),
                servicePath);

            _outStockScanQueryEndpoint = NormalizeRelative(
                configLoader.GetApiPath("mold.outStockScanQuery", "/pda/mold/outStockScanQuery"),
                servicePath);

            _outStockConfirmEndpoint = NormalizeRelative(
                configLoader.GetApiPath("mold.outStock", "/pda/mold/outStock"),
                servicePath);
        }

        // ========== 手动拼接工具：BaseAddress.AbsoluteUri + 相对端点 ==========
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

            var relative = url.TrimStart('/');
            return baseUrl + relative;
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

        // ===================== 工单分页 =====================
        public async Task<WorkOrderPageResult> GetMoldsAsync(MoldQuery q, CancellationToken ct = default)
        {
            var p = new Dictionary<string, string>
            {
                ["pageNo"] = q.PageNo.ToString(),
                ["pageSize"] = q.PageSize.ToString()
            };
            if (q.CreatedTimeStart.HasValue) p["createdTimeStart"] = q.CreatedTimeStart.Value.ToString("yyyy-MM-dd HH:mm:ss");
            if (q.CreatedTimeEnd.HasValue) p["createdTimeEnd"] = q.CreatedTimeEnd.Value.ToString("yyyy-MM-dd HH:mm:ss");
            if (!string.IsNullOrWhiteSpace(q.MoldNo)) p["workOrderNo"] = q.MoldNo!.Trim();
            if (!string.IsNullOrWhiteSpace(q.MaterialName)) p["materialName"] = q.MaterialName!.Trim();

            string qs = BuildQuery(p);
            var url = _pageEndpoint + "?" + qs;
            var full = BuildFullUrl(_http.BaseAddress, url);

            using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(full, UriKind.Absolute));
            System.Diagnostics.Debug.WriteLine("[MoldApi] GET " + full);

            using var httpResp = await _http.SendAsync(req, ct);
            var json = await httpResp.Content.ReadAsStringAsync(ct);
            System.Diagnostics.Debug.WriteLine("[MoldApi] Resp: " + json[..Math.Min(300, json.Length)] + "...");

            if (!httpResp.IsSuccessStatusCode)
                return new WorkOrderPageResult { success = false, message = $"HTTP {(int)httpResp.StatusCode}" };

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var resp = JsonSerializer.Deserialize<WorkOrderPageResult>(json, options) ?? new WorkOrderPageResult();

            // 兼容 result.records
            var nested = resp.result?.records;
            if (nested is not null && resp.result is not null)
            {
                if (resp.result.records is null || resp.result.records.Count == 0)
                    resp.result.records = nested;
                if (resp.result.pageNo == 0) resp.result.pageNo = resp.result.list.pageNo;
                if (resp.result.pageSize == 0) resp.result.pageSize = resp.result.list.pageSize;
                if (resp.result.total == 0) resp.result.total = resp.result.list.total;
            }

            return resp;
        }

        /// <summary>工单流程：/getMoldWorkflow?id=...</summary>
        public async Task<WorkflowResp?> GetMoldWorkflowAsync(string id, CancellationToken ct = default)
        {
            var p = new Dictionary<string, string> { ["id"] = id?.Trim() ?? "" };
            var url = _workflowEndpoint + "?" + BuildQuery(p);
            var full = BuildFullUrl(_http.BaseAddress, url);

            using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(full, UriKind.Absolute));
            System.Diagnostics.Debug.WriteLine("[MoldApi] GET " + full);

            using var httpResp = await _http.SendAsync(req, ct);
            var json = await httpResp.Content.ReadAsStringAsync(ct);
            System.Diagnostics.Debug.WriteLine("[MoldApi] Resp(getMoldWorkflow): " + json[..Math.Min(300, json.Length)] + "...");

            if (!httpResp.IsSuccessStatusCode)
                return new WorkflowResp { success = false, message = $"HTTP {(int)httpResp.StatusCode}" };

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var resp = JsonSerializer.Deserialize<WorkflowResp>(json, options) ?? new WorkflowResp();
            return resp;
        }

        /// <summary>
        /// 工序分页：/pageWorkProcessTasks?pageNo=&pageSize=&workOrderNo=
        /// 返回分页结构，数据在 result.records[]
        /// </summary>
        public async Task<PageResp<ProcessTask>?> PageWorkProcessTasksAsync(
            string workOrderNo, int pageNo = 1, int pageSize = 50, CancellationToken ct = default)
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
            System.Diagnostics.Debug.WriteLine("[MoldApi] GET " + full);

            using var httpResp = await _http.SendAsync(req, ct);
            var json = await httpResp.Content.ReadAsStringAsync(ct);
            System.Diagnostics.Debug.WriteLine("[MoldApi] Resp(pageWorkProcessTasks): " + json[..Math.Min(300, json.Length)] + "...");

            if (!httpResp.IsSuccessStatusCode)
                return new PageResp<ProcessTask> { success = false, message = $"HTTP {(int)httpResp.StatusCode}" };

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var resp = JsonSerializer.Deserialize<PageResp<ProcessTask>>(json, options) ?? new PageResp<ProcessTask>();

            var nested = resp.result?.records ?? resp.result?.records;
            if (nested is not null && resp.result is not null)
            {
                if (resp.result.records is null || resp.result.records.Count == 0)
                    resp.result.records = nested;

                if (resp.result.pageNo == 0 && resp.result is not null) resp.result.pageNo = resp.result.pageNo;
                if (resp.result.pageSize == 0 && resp.result is not null) resp.result.pageSize = resp.result.pageSize;
                if (resp.result.total == 0 && resp.result is not null) resp.result.total = resp.result.total;
            }

            return resp;
        }

        static int ToInt(decimal? v) => v.HasValue ? (int)Math.Round(v.Value, MidpointRounding.AwayFromZero) : 0;

        public async Task<IReadOnlyList<InboundScannedRow>> GetInStockScanDetailAsync(string instockId, CancellationToken ct = default)
        {
            var url = $"{_scanDetailEndpoint}?InstockId={Uri.EscapeDataString(instockId)}";
            var full = BuildFullUrl(_http.BaseAddress, url);

            using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(full, UriKind.Absolute));
            using var res = await _http.SendAsync(req, ct).ConfigureAwait(false);
            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var dto = JsonSerializer.Deserialize<GetInStockScanDetailResp>(json, opt);

            if (dto?.success != true || dto.result is null || dto.result.Count == 0)
                return Array.Empty<InboundScannedRow>();

            static int ToIntSafe(string? s)
            {
                if (string.IsNullOrWhiteSpace(s)) return 0;
                s = s.Trim().Replace(",", "");
                return int.TryParse(s, out var v) ? v : 0;
            }

            var list = dto.result.Select(x => new InboundScannedRow(
                Barcode: (x.barcode ?? string.Empty).Trim(),
                DetailId: (x.id ?? string.Empty).Trim(),
                Location: (x.location ?? string.Empty).Trim(),
                MaterialName: (x.materialName ?? string.Empty).Trim(),
                Qty: ToInt(x.qty),
                Spec: (x.spec ?? string.Empty).Trim(),
                ScanStatus: x.scanStatus ?? false,
                WarehouseCode: x.warehouseCode?.Trim()
            )).ToList();

            return list;
        }

        // ========= 扫码入库实现 =========
        public async Task<SimpleOk> InStockByBarcodeAsync(string instockId, string barcode, CancellationToken ct = default)
        {
            var body = JsonSerializer.Serialize(new { barcode, instockId });
            var full = BuildFullUrl(_http.BaseAddress, _scanByBarcodeEndpoint);

            using var req = new HttpRequestMessage(HttpMethod.Post, new Uri(full, UriKind.Absolute))
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            using var res = await _http.SendAsync(req, ct);
            var json = await res.Content.ReadAsStringAsync(ct);
            var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var dto = JsonSerializer.Deserialize<ScanByBarcodeResp>(json, opt);

            var ok = dto?.success == true;
            return new SimpleOk(ok, dto?.message);
        }

        // =============== 扫描查询（GET） ===============
        public async Task<MoldScanQueryResp?> InStockScanQueryAsync(string code, CancellationToken ct = default)
        {
            var url = $"{_scanQueryEndpoint}?code={Uri.EscapeDataString(code ?? string.Empty)}";
            var full = BuildFullUrl(_http.BaseAddress, url);

            using var res = await _http.GetAsync(new Uri(full, UriKind.Absolute), ct);
            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<MoldScanQueryResp>(
                json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        // =============== 确认入库（POST） ===============
        public async Task<SimpleOk> ConfirmInStockByListAsync(InStockConfirmReq req, CancellationToken ct = default)
        {
            var body = JsonSerializer.Serialize(req);
            var full = BuildFullUrl(_http.BaseAddress, _confirmInStockEndpoint);

            using var msg = new HttpRequestMessage(HttpMethod.Post, new Uri(full, UriKind.Absolute))
            { Content = new StringContent(body, Encoding.UTF8, "application/json") };

            using var res = await _http.SendAsync(msg, ct);
            var txt = await res.Content.ReadAsStringAsync(ct);

            var dto = JsonSerializer.Deserialize<ConfirmResp>(
                txt, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var ok = dto?.success == true && dto.result == true;
            return new SimpleOk(ok, dto?.message ?? (ok ? "确认入库成功" : "确认入库失败"));
        }

        //------------------------------- 出库：页面视图 -------------------------------
        /// <summary>
        /// 把 /pda/mold/queryForWorkOrder 的返回映射为页面所需分组：
        /// 一级：ModelCode + BaseQty；二级：MoldNumbers（取资源分配表里的 resourceCode）
        /// </summary>
        public async Task<WorkOrderMoldView> GetViewAsync(string workOrderNo, CancellationToken ct = default)
        {
            var raw = await GetRawAsync(workOrderNo, ct).ConfigureAwait(false);
            var result = raw?.result;

            var view = new WorkOrderMoldView
            {
                WorkOrderNo = result?.workOrderNo?.Trim() ?? workOrderNo?.Trim() ?? string.Empty,
                MaterialName = result?.materialName?.Trim() ?? string.Empty,   // ← 与页面 VM 对齐
            };

            // 左侧：型号 + 基础需求数量（pdaBasMoldInfoDTOS）
            var models = result?.pdaBasMoldInfoDTOS ?? new List<PdaBasMoldInfoDTO>();

            // 右侧：资源分配（按 model 分组，取 resourceCode 作为“模具编码”）
            var allocs = result?.planProcessRouteResourceAllocationDTOS ?? new List<PlanProcessRouteResourceAllocationDTO>();
            var byModel = allocs
                .Where(a => !string.IsNullOrWhiteSpace(a?.model))
                .GroupBy(a => a!.model!.Trim())
                .ToDictionary(
                    g => g.Key,
                    g => g.Where(x => !string.IsNullOrWhiteSpace(x.resourceCode))
                          .Select(x => x.resourceCode!.Trim())
                          .Distinct()
                          .ToList()
                );

            foreach (var m in models)
            {
                var modelCode = (m.moldModel ?? m.moldCode ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(modelCode)) continue;

                byModel.TryGetValue(modelCode, out var codes);
                codes ??= new List<string>();

                var baseQty = m.baseDemandQty > 0 ? m.baseDemandQty : m.demandQty;

                view.Models.Add(new MoldModelView
                {
                    ModelCode = modelCode,
                    BaseQty = baseQty,
                    MoldNumbers = codes
                });
            }

            // 没有左侧数据但有右侧，也要把右侧补上（极端容错）
            if (view.Models.Count == 0 && byModel.Count > 0)
            {
                foreach (var kv in byModel)
                {
                    view.Models.Add(new MoldModelView
                    {
                        ModelCode = kv.Key,
                        BaseQty = kv.Value.Count,
                        MoldNumbers = kv.Value
                    });
                }
            }

            return view;
        }

        /// <summary>底层：按工单号取原始视图数据</summary>
        public async Task<QueryForWorkOrderResp?> GetRawAsync(string workOrderNo, CancellationToken ct = default)
        {
            var url = $"{_queryForWorkOrderEndpoint}?workOrderNo={Uri.EscapeDataString(workOrderNo ?? string.Empty)}";
            var requestUrl = BuildFullUrl(_http.BaseAddress, url);

            using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(requestUrl, UriKind.Absolute));
            using var res = await _http.SendAsync(req, ct).ConfigureAwait(false);

            if (!res.IsSuccessStatusCode)
            {
                var head = await PeekAsync(res.Content, 1024, ct).ConfigureAwait(false);
                throw new HttpRequestException($"GET {requestUrl} -> {(int)res.StatusCode}. Head: {head}");
            }

            var txt = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<QueryForWorkOrderResp>(txt,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        private static async Task<string> PeekAsync(HttpContent content, int maxLen, CancellationToken ct)
        {
            var txt = await content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(txt)) return "";
            return txt.Length <= maxLen ? txt : txt.Substring(0, maxLen) + "...";
        }

        //------------------------------- 出库：扫描/确认 -------------------------------
        public async Task<MoldOutScanQueryResp?> OutStockScanQueryAsync(string code, string workOrderNo, CancellationToken ct = default)
        {
            var url = $"{_outStockScanQueryEndpoint}?code={Uri.EscapeDataString(code ?? "")}&workOrderNo={Uri.EscapeDataString(workOrderNo ?? "")}";
            var full = BuildFullUrl(_http.BaseAddress, url);

            using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(full, UriKind.Absolute));
            using var res = await _http.SendAsync(req, ct).ConfigureAwait(false);
            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<MoldOutScanQueryResp>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public async Task<SimpleOk> ConfirmOutStockAsync(MoldOutConfirmReq req, CancellationToken ct = default)
        {
            var full = BuildFullUrl(_http.BaseAddress, _outStockConfirmEndpoint);

            var json = JsonSerializer.Serialize(req, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            using var http = new HttpRequestMessage(HttpMethod.Post, new Uri(full, UriKind.Absolute))
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using var res = await _http.SendAsync(http, ct).ConfigureAwait(false);
            var txt = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!res.IsSuccessStatusCode)
                return new SimpleOk(false, $"HTTP {(int)res.StatusCode}: {txt}");

            var dto = JsonSerializer.Deserialize<ConfirmResp>(txt,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var ok = dto?.success == true && (dto.result is bool b ? b : true);
            return new SimpleOk(ok, dto?.message ?? (ok ? "出库成功" : "出库失败"));
        }
    }




    // ===================== 其余分页/流程模型（保持你原有定义即可） =====================
    // WorkOrderPageResult / WorkflowResp / PageResp<T> / ProcessTask / ConfirmResp / GetInStockScanDetailResp 等
    // 如果这些类不在本文件，请保持引用不变；若你此前就写在本文件，继续保留即可。
}
