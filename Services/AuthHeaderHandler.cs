using System.Net.Http.Headers;

namespace JXHLJSApp.Services;

/// <summary>
/// 为业务接口统一补充认证头。
/// 同时发送标准 Authorization Bearer 与常见后端 token 头，兼容不同网关/后端鉴权实现。
/// </summary>
public sealed class AuthHeaderHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri?.AbsolutePath?.ToLowerInvariant() ?? string.Empty;
        if (path.Contains("/auth/login") || path.Contains("/auth/refresh"))
        {
            return await base.SendAsync(request, ct).ConfigureAwait(false);
        }

        var token = TokenStorage.NormalizeToken(await TokenStorage.LoadAsync().ConfigureAwait(false));
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Remove("X-Access-Token");
            request.Headers.Remove("token");
            request.Headers.TryAddWithoutValidation("X-Access-Token", token);
            request.Headers.TryAddWithoutValidation("token", token);
        }

        return await base.SendAsync(request, ct).ConfigureAwait(false);
    }
}
