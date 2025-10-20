using IndustrialControlMAUI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace IndustrialControlMAUI.Services
{
    public interface IAuthApi
    {
        Task<List<UserInfoDto>> GetAllUsersAsync(CancellationToken ct = default);
    }
    public sealed class AuthApi : IAuthApi
    {
        private readonly HttpClient _http;
        private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

        public AuthApi(HttpClient http) => _http = http;

        public async Task<List<UserInfoDto>> GetAllUsersAsync(CancellationToken ct = default)
        {
            // 兼容你们“有无斜杠”的 BaseAddress，确保路径为 /normalService/pda/auth/allUsers
            var url = new Uri(_http.BaseAddress!, "/normalService/pda/auth/allUsers");

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();

            await using var s = await resp.Content.ReadAsStreamAsync(ct);
            var data = await JsonSerializer.DeserializeAsync<ApiResp<List<UserInfoDto>>>(s, _json, ct)
                       ?? new ApiResp<List<UserInfoDto>>();

            var list = data.result ?? new();

            // 去重（以 username 为主键；为空则用 id）
            var dedup = list
                .Where(u => !string.IsNullOrWhiteSpace(u.username) || !string.IsNullOrWhiteSpace(u.id))
                .GroupBy(u => string.IsNullOrWhiteSpace(u.username) ? u.id : u.username, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            return dedup;
        }
    }

}
