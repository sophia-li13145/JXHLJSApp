using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using JXHLJSApp.Models;
using JXHLJSApp.Services.Common;
using JXHLJSApp.Tools;
using Serilog;

namespace JXHLJSApp.Services;

public sealed class IncomingStockService : IIncomingStockService
{
    private readonly HttpClient _http;
    private readonly AuthState _auth;
    private readonly string _parseIncomingEndpoint;
    private readonly string _submitPendingEndpoint;

    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IncomingStockService(HttpClient http, IConfigLoader configLoader, AuthState auth)
    {
        _http = http;
        _auth = auth;

        var baseUrl = configLoader.GetBaseUrl();
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri(baseUrl, UriKind.Absolute);

        var servicePath = _http.BaseAddress.AbsolutePath?.TrimEnd('/') ?? "/normalService";

        _http.DefaultRequestHeaders.Accept.Clear();
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _parseIncomingEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("incoming.parseBarcode", "/pda/wmsMaterialInstock/parseIncomingBarcode"),
            servicePath);
        _submitPendingEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("incoming.submitPending", "/pda/wmsMaterialInstock/submitPendingStock"),
            servicePath);
    }

    public async Task<IncomingBarcodeParseResult?> ParseIncomingBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        var dto = await PostJsonAsync<object, IncomingBarcodeParseResponse>(
            _parseIncomingEndpoint, new { barcode }, ct).ConfigureAwait(false);

        return dto?.result;
    }

    public async Task<SimpleOk> SubmitPendingStockAsync(IEnumerable<IncomingPendingStockRequest> items, CancellationToken ct = default)
    {
        var payload = items?.ToArray() ?? Array.Empty<IncomingPendingStockRequest>();
        var dto = await PostJsonAsync<IncomingPendingStockRequest[], IncomingSubmitPendingResponse>(
            _submitPendingEndpoint, payload, ct).ConfigureAwait(false);

        var ok = dto?.success == true || dto?.result == true;
        return new SimpleOk(ok, dto?.message);
    }

    private static void EnsureJson(HttpResponseMessage res)
    {
        var mt = res.Content.Headers.ContentType?.MediaType ?? "";
        if (!mt.Contains("json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"期望 JSON，实际返回 Content-Type: {mt}");
    }

    private async Task<TResp?> PostJsonAsync<TReq, TResp>(string url, TReq body, CancellationToken ct)
    {
        var requestUrl = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, url);
        var json = JsonSerializer.Serialize(body, JsonOpt);
        using var req = new HttpRequestMessage(HttpMethod.Post, new Uri(requestUrl, UriKind.Absolute))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var res = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var raw = await ResponseGuard.ReadAsStringAndCheckAsync(res, _auth, ct).ConfigureAwait(false);

        if (!res.IsSuccessStatusCode)
        {
            Log.Warning("POST {Url} 非 2xx：{Status} 预览：{Head}", requestUrl, (int)res.StatusCode, raw);
            res.EnsureSuccessStatusCode();
        }

        EnsureJson(res);
        return JsonSerializer.Deserialize<TResp>(raw, JsonOpt);
    }
}
