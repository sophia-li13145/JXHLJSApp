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
    private readonly IConfigLoader _configLoader;
    private Uri? _baseAddress;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private string _loginEndpoint = string.Empty;
    private string _allUserEndpoint = string.Empty;

    public AuthApi(HttpClient http, IConfigLoader configLoader)
    {
        _http = http;
        _configLoader = configLoader;
        if (_http.Timeout == Timeout.InfiniteTimeSpan)
        {
            _http.Timeout = TimeSpan.FromSeconds(15);
        }

        _http.DefaultRequestHeaders.Accept.Clear();
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        ApplyLatestConfig();
    }

    public async Task<LoginResult> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        ApplyLatestConfig();
        var full = ServiceUrlHelper.BuildFullUrl(_baseAddress, _loginEndpoint);
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
        ApplyLatestConfig();
        var full = ServiceUrlHelper.BuildFullUrl(_baseAddress, _allUserEndpoint);
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

    private void ApplyLatestConfig()
    {
        var baseUrl = _configLoader.GetBaseUrl();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("配置文件缺少有效的 BaseUrl。");
        }

        if (baseUrl.EndsWith("/", StringComparison.Ordinal))
        {
            baseUrl = baseUrl.TrimEnd('/');
        }

        _baseAddress = new Uri(baseUrl, UriKind.Absolute);

        var servicePath = _baseAddress.AbsolutePath?.TrimEnd('/') ?? "/normalService";
        _loginEndpoint = ServiceUrlHelper.NormalizeRelative(
            _configLoader.GetApiPath("login", "/pda/auth/login"), servicePath);
        _allUserEndpoint = ServiceUrlHelper.NormalizeRelative(
            _configLoader.GetApiPath("auth.alluser", "/pda/auth/allUsers"), servicePath);
    }
}

public sealed record LoginRequest(string username, string password);

public sealed record LoginResult(bool Success, string? Token, string? Message, UserInfoDto? UserInfo);

public sealed class LoginResponseResult
{
    public string? token { get; set; }
    public UserInfoDto? userInfo { get; set; }
}
