using JXHLJSApp.Models;
using JXHLJSApp.Services.Common;
using System.Net.Http.Json;
using System.Text.Json;

namespace JXHLJSApp.Services.Transport;

public interface ITransportOrderApi
{
    Task<TransportOrderDto> ScanTransportOrderAsync(string qrCode, CancellationToken ct = default);
    Task<bool> CompleteTransportOrderAsync(string transportOrderNo, CancellationToken ct = default);
}

public sealed class TransportOrderApi : ITransportOrderApi
{
    private readonly HttpClient _http;
    private readonly string _scanEndpoint;
    private readonly string _completeEndpoint;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public TransportOrderApi(HttpClient http, IConfigLoader configLoader)
    {
        _http = http;
        var servicePath = _http.BaseAddress?.AbsolutePath?.TrimEnd('/') ?? "/jxhljszpService";
        _scanEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("transportOrder.scan", "/pda/transportOrder/scanTransportOrder"), servicePath);
        _completeEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("transportOrder.complete", "/pda/transportOrder/completeTransportOrder"), servicePath);
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
