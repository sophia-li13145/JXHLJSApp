using JXHLJSApp.Models;
using JXHLJSApp.Services.Common;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace JXHLJSApp.Services;

public interface IAuthApi
{
    Task<LoginResult> LoginAsync(string username, string password, CancellationToken ct = default);
    Task<List<UserInfoDto>> GetAllUsersAsync(CancellationToken ct = default);
}

public sealed class AuthApi : IAuthApi
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _loginEndpoint;
    private readonly string _allUserEndpoint;

    public AuthApi(HttpClient http, IConfigLoader configLoader)
    {
        _http = http;
        if (_http.Timeout == Timeout.InfiniteTimeSpan)
        {
            _http.Timeout = TimeSpan.FromSeconds(15);
        }

        var baseUrl = configLoader.GetBaseUrl();
        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
        }

        var servicePath = _http.BaseAddress.AbsolutePath?.TrimEnd('/') ?? "/normalService";

        _http.DefaultRequestHeaders.Accept.Clear();
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _loginEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("login", "/pda/auth/login"), servicePath);
        _allUserEndpoint = ServiceUrlHelper.NormalizeRelative(
            configLoader.GetApiPath("auth.alluser", "/pda/auth/allUsers"), servicePath);
    }

    public async Task<LoginResult> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var full = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _loginEndpoint);
        using var req = new HttpRequestMessage(HttpMethod.Post, full)
        {
            Content = JsonContent.Create(new LoginRequest(username, password), options: JsonOptions)
        };

        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
        {
            return new LoginResult(false, null, $"登录接口异常（HTTP {(int)resp.StatusCode}）", null);
        }

        var data = JsonSerializer.Deserialize<ApiResp<LoginResponseResult>>(body, JsonOptions);
        var token = data?.result?.token;
        var success = data?.success == true || data?.code == 0;

        return new LoginResult(success, token, data?.message, data?.result?.userInfo);
    }

    public async Task<List<UserInfoDto>> GetAllUsersAsync(CancellationToken ct = default)
    {
        var full = ServiceUrlHelper.BuildFullUrl(_http.BaseAddress, _allUserEndpoint);
        using var req = new HttpRequestMessage(HttpMethod.Get, full);
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        await using var s = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ApiResp<List<UserInfoDto>>>(s, JsonOptions, ct).ConfigureAwait(false)
                   ?? new ApiResp<List<UserInfoDto>>();

        var list = data.result ?? new();

        return list
            .Where(u => !string.IsNullOrWhiteSpace(u.username) || !string.IsNullOrWhiteSpace(u.id))
            .GroupBy(u => string.IsNullOrWhiteSpace(u.username) ? u.id : u.username, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }
}

public sealed record LoginRequest(string username, string password);

public sealed record LoginResult(bool Success, string? Token, string? Message, UserInfoDto? UserInfo);

public sealed class LoginResponseResult
{
    public string? token { get; set; }
    public UserInfoDto? userInfo { get; set; }
}
