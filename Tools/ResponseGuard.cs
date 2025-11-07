using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace IndustrialControlMAUI.Tools
{
    public static class ResponseGuard
    {
        private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };
        private static readonly HashSet<string> _expiredCodes = new(StringComparer.OrdinalIgnoreCase)
    { "401", "40101", "40301", "TOKEN_EXPIRED", "NO_AUTH" };

        public static async Task<string> ReadAsStringAndCheckAsync(HttpResponseMessage res, AuthState auth, CancellationToken ct)
        {
            var text = await res.Content.ReadAsStringAsync(ct);

            if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                _ = auth.LogoutAsync($"登录状态失效（HTTP {(int)res.StatusCode}）");
                return text;
            }

            try
            {
                var api = JsonSerializer.Deserialize<ApiBase>(text, _json);
                if (api?.success == false)
                {
                    var code = api.code?.ToString() ?? "";
                    if (_expiredCodes.Contains(code) || code.StartsWith("401", StringComparison.OrdinalIgnoreCase)
                        || code.Contains("EXPIRE", StringComparison.OrdinalIgnoreCase))
                    {
                        _ = auth.LogoutAsync(api?.message ?? "登录状态失效");
                    }
                }
            }
            catch { /* ignore */ }

            return text;
        }

        private sealed class ApiBase
        {
            public bool? success { get; set; }
            public object? code { get; set; }
            public string? message { get; set; }
        }
    }
}
