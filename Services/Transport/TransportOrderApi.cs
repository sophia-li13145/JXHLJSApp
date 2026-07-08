using JXHLJSApp.Models;
using JXHLJSApp.Services.Common;
using System.Net.Http.Json;
using System.Text.Json;

namespace JXHLJSApp.Services.Transport;

public interface ITransportOrderApi
{
    Task<TransportOrderDto> ScanTransportOrderAsync(string qrCode, CancellationToken ct = default);
    Task<bool> CompleteTransportOrderAsync(string transportOrderNo, CancellationToken ct = default);
    Task<List<MaterialOutstockTransportOrderDto>> GetMaterialOutstockTransportOrdersAsync(CancellationToken ct = default);
    Task<MaterialOutstockTransportOrderDetailDto> GetMaterialOutstockTransportOrderDetailAsync(string transportOrderNo, CancellationToken ct = default);
    Task<List<ProductInstockTransportOrderDto>> GetProductInstockTransportOrdersAsync(CancellationToken ct = default);
    Task<ProductInstockTransportOrderDetailDto> GetProductInstockTransportOrderDetailAsync(string transportOrderNo, CancellationToken ct = default);
}

public sealed class TransportOrderApi : ITransportOrderApi
{
    private readonly HttpClient _http;
    private readonly string _scanEndpoint;
    private readonly string _completeEndpoint;
    private readonly string _outstockListEndpoint;
    private readonly string _outstockDetailEndpoint;
    private readonly string _productInstockListEndpoint;
    private readonly string _productInstockDetailEndpoint;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public TransportOrderApi(HttpClient http, IConfigLoader configLoader)
    {
        _http = http;
        var servicePath = _http.BaseAddress?.AbsolutePath?.TrimEnd('/') ?? "/jxhljszpService";
        _scanEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("transportOrder.scan", "/pda/transportOrder/scanTransportOrder"), servicePath);
        _completeEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("transportOrder.complete", "/pda/transportOrder/completeTransportOrder"), servicePath);
        _outstockListEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("transportOrder.listMaterialOutstockTransportOrders", "/pda/transportOrder/listMaterialOutstockTransportOrders"), servicePath);
        _outstockDetailEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("transportOrder.detailMaterialOutstockTransportOrder", "/pda/transportOrder/detailMaterialOutstockTransportOrder"), servicePath);
        _productInstockListEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("transportOrder.listProductInstockTransportOrders", "/pda/transportOrder/listProductInstockTransportOrders"), servicePath);
        _productInstockDetailEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("transportOrder.detailProductInstockTransportOrder", "/pda/transportOrder/detailProductInstockTransportOrder"), servicePath);
    }

    public async Task<TransportOrderDto> ScanTransportOrderAsync(string qrCode, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _scanEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, new { qrCode }, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<TransportOrderDto>>(stream, JsonOptions, ct).ConfigureAwait(false);
        EnsureApiSuccess(data);
        return data?.result ?? new TransportOrderDto();
    }

    public async Task<List<MaterialOutstockTransportOrderDto>> GetMaterialOutstockTransportOrdersAsync(CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _outstockListEndpoint);
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<List<MaterialOutstockTransportOrderDto>>>(stream, JsonOptions, ct).ConfigureAwait(false);
        EnsureApiSuccess(data);
        return data?.result ?? new List<MaterialOutstockTransportOrderDto>();
    }

    public async Task<MaterialOutstockTransportOrderDetailDto> GetMaterialOutstockTransportOrderDetailAsync(string transportOrderNo, CancellationToken ct = default)
    {
        var endpoint = BuildUrlWithQuery(_outstockDetailEndpoint, new Dictionary<string, string?>
        {
            [nameof(transportOrderNo)] = transportOrderNo
        });
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, endpoint);
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<MaterialOutstockTransportOrderDetailDto>>(stream, JsonOptions, ct).ConfigureAwait(false);
        EnsureApiSuccess(data);
        return data?.result ?? new MaterialOutstockTransportOrderDetailDto();
    }



    public async Task<List<ProductInstockTransportOrderDto>> GetProductInstockTransportOrdersAsync(CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _productInstockListEndpoint);
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<List<ProductInstockTransportOrderDto>>>(stream, JsonOptions, ct).ConfigureAwait(false);
        EnsureApiSuccess(data);
        return data?.result ?? new List<ProductInstockTransportOrderDto>();
    }

    public async Task<ProductInstockTransportOrderDetailDto> GetProductInstockTransportOrderDetailAsync(string transportOrderNo, CancellationToken ct = default)
    {
        var endpoint = BuildUrlWithQuery(_productInstockDetailEndpoint, new Dictionary<string, string?>
        {
            [nameof(transportOrderNo)] = transportOrderNo
        });
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, endpoint);
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<ProductInstockTransportOrderDetailDto>>(stream, JsonOptions, ct).ConfigureAwait(false);
        EnsureApiSuccess(data);
        return data?.result ?? new ProductInstockTransportOrderDetailDto();
    }

    public async Task<bool> CompleteTransportOrderAsync(string transportOrderNo, CancellationToken ct = default)
    {
        var url = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _completeEndpoint);
        using var resp = await _http.PostAsJsonAsync(url, new { transportOrderNo }, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<JsonElement?>>(stream, JsonOptions, ct).ConfigureAwait(false);
        EnsureApiSuccess(data);
        return ReadFlexibleBooleanResult(data);
    }

    private static string BuildUrlWithQuery(string endpoint, IReadOnlyDictionary<string, string?> query)
    {
        var pairs = query
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}")
            .ToArray();
        return pairs.Length == 0 ? endpoint : $"{endpoint}?{string.Join("&", pairs)}";
    }

    private static void EnsureApiSuccess<T>(ApiResp<T>? response)
    {
        if (response?.success == true) return;
        throw new TransportOrderApiException(string.IsNullOrWhiteSpace(response?.message) ? "接口返回失败，请稍后重试。" : response!.message!);
    }

    private static bool ReadFlexibleBooleanResult(ApiResp<JsonElement?>? response)
    {
        if (response?.result is not { } result) return response?.success == true;
        return result.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => result.TryGetInt32(out var value) && value != 0,
            JsonValueKind.String => bool.TryParse(result.GetString(), out var value) ? value : response?.success == true,
            JsonValueKind.Null or JsonValueKind.Undefined => response?.success == true,
            _ => response?.success == true
        };
    }
}

public sealed class TransportOrderApiException : Exception
{
    public TransportOrderApiException(string message) : base(message)
    {
    }
}
