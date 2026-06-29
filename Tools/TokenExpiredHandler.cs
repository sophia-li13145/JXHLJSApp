using JXHLJSApp;
using System.Net;
using System.Text;
using System.Text.Json;

namespace JXHLJSApp.Tools
{
    /// <summary>
    /// 统一拦截 HTTP 401/403 或业务 JSON 返回的 400/4001/401 等 token 失效码，并触发退出登录。
    /// </summary>
    public sealed class TokenExpiredHandler : DelegatingHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
        private readonly AuthState _auth;

        public TokenExpiredHandler(AuthState auth) => _auth = auth;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri?.AbsolutePath?.ToLowerInvariant() ?? string.Empty;
            var skipAuth = path.Contains("/auth/login") || path.Contains("/auth/refresh");

            var resp = await base.SendAsync(request, ct).ConfigureAwait(false);

            // 登录/刷新等接口排除，避免登录失败时也触发 Logout。
            if (skipAuth) return resp;

            if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                await _auth.LogoutAsync($"登录状态失效（HTTP {(int)resp.StatusCode}）").ConfigureAwait(false);
                return resp;
            }

            await CheckBusinessTokenExpiredAsync(resp, ct).ConfigureAwait(false);
            return resp;
        }

        private async Task CheckBusinessTokenExpiredAsync(HttpResponseMessage resp, CancellationToken ct)
        {
            if (resp.Content is null) return;

            var mediaType = resp.Content.Headers.ContentType?.MediaType;
            var text = await ResponseGuard.ReadAsStringSafeAsync(resp.Content, ct).ConfigureAwait(false);
            RestoreContent(resp, text, mediaType);

            if (!LooksLikeJson(mediaType, text)) return;

            try
            {
                var api = JsonSerializer.Deserialize<ApiBase>(text, JsonOptions);
                var code = api?.code?.ToString();
                if (api?.success == false && IsTokenExpiredCode(code))
                {
                    await _auth.LogoutAsync(api.message ?? "登录状态已过期").ConfigureAwait(false);
                }
            }
            catch
            {
                // 非标准 JSON 或接口返回结构异常时不影响业务层继续处理原始响应。
            }
        }

        private static void RestoreContent(HttpResponseMessage resp, string text, string? mediaType)
        {
            resp.Content = new StringContent(text, Encoding.UTF8, mediaType ?? "application/json");
        }

        private static bool LooksLikeJson(string? mediaType, string? text)
        {
            if (!string.IsNullOrWhiteSpace(mediaType) && mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var value = text?.TrimStart();
            return value?.StartsWith('{') == true || value?.StartsWith('[') == true;
        }

        private static bool IsTokenExpiredCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;

            var value = code.Trim();
            return value is "400" or "4001" or "401" or "40101" or "40301" or "TOKEN_EXPIRED" or "NO_AUTH"
                   || value.StartsWith("4001", StringComparison.OrdinalIgnoreCase)
                   || value.StartsWith("401", StringComparison.OrdinalIgnoreCase)
                   || value.Contains("TOKEN", StringComparison.OrdinalIgnoreCase)
                   || value.Contains("EXPIRE", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class ApiBase
        {
            public bool? success { get; set; }
            public object? code { get; set; }
            public string? message { get; set; }
        }
    }
}
