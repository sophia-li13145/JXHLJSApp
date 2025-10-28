using IndustrialControlMAUI.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace IndustrialControlMAUI.Services
{
    // ===================== 接口定义 =====================
    public interface IAttachmentApi
    {
        Task<ApiResp<UploadAttachmentResult>> UploadAttachmentAsync(
                string attachmentFolder,
                string attachmentLocation,
                Stream fileStream,                // ← 新增：文件流
                string fileName,                  // ← 新增：文件名（需含后缀）
                string? contentType = null,       // ← 可选：MIME 类型
                string? attachmentName = null,
                string? attachmentExt = null,
                long? attachmentSize = null,
                CancellationToken ct = default);

        Task<ApiResp<string>> GetPreviewUrlAsync(string attachmentUrl, long? expires = null, CancellationToken ct = default);
      
    }

    // ===================== 实现 =====================
    public class AttachmentApi : IAttachmentApi
    {
        private readonly HttpClient _http;
        private readonly string _uploadAttachmentPath;
        private readonly string _previewImagePath;

        private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

        public AttachmentApi(HttpClient http, IConfigLoader configLoader)
        {
            _http = http;
            if (_http.Timeout == System.Threading.Timeout.InfiniteTimeSpan)
                _http.Timeout = TimeSpan.FromSeconds(15);

            var baseUrl = configLoader.GetBaseUrl();
            if (_http.BaseAddress is null)
                _http.BaseAddress = new Uri(baseUrl, UriKind.Absolute);

            var servicePath = _http.BaseAddress.AbsolutePath?.TrimEnd('/') ?? "/normalService";
            _http.DefaultRequestHeaders.Accept.Clear();
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _uploadAttachmentPath = NormalizeRelative(
               configLoader.GetApiPath("quality.uploadAttachment", "/pda/attachment/uploadAttachment"), servicePath);
            _previewImagePath = NormalizeRelative(
    configLoader.GetApiPath("quality.previewImage", "/pda/attachment/previewAttachment"),
    servicePath);
           
        }
        // ===== 公共工具 =====
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

            return baseUrl + url.TrimStart('/');
        }


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

      
        public async Task<ApiResp<UploadAttachmentResult>> UploadAttachmentAsync(
            string attachmentFolder,
            string attachmentLocation,
            Stream fileStream,                // ← 新增：文件流
            string fileName,                  // ← 新增：文件名（需含后缀）
            string? contentType = null,       // ← 可选：MIME 类型
            string? attachmentName = null,
            string? attachmentExt = null,
            long? attachmentSize = null,
            CancellationToken ct = default)
        {
            var url = BuildFullUrl(_http.BaseAddress, _uploadAttachmentPath);

            // 准备 multipart/form-data
            using var form = new MultipartFormDataContent();

            // 1) 文件部分（字段名要与后端匹配，常见是 "file" 或文档指定的名）
            var fileContent = new StreamContent(fileStream);
            if (!string.IsNullOrWhiteSpace(contentType))
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

            // 关键：name 要与后端参数名一致（例如 "file"），并且一定要提供 filename
            form.Add(fileContent, "file", fileName);

            // 2) 其他普通字段（与后端参数名一一对应）
            form.Add(new StringContent(attachmentFolder), "attachmentFolder");
            form.Add(new StringContent(attachmentLocation), "attachmentLocation");

            if (!string.IsNullOrWhiteSpace(attachmentName))
                form.Add(new StringContent(attachmentName), "attachmentName");

            if (!string.IsNullOrWhiteSpace(attachmentExt))
                form.Add(new StringContent(attachmentExt), "attachmentExt");

            if (attachmentSize.HasValue)
                form.Add(new StringContent(attachmentSize.Value.ToString()), "attachmentSize");

            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
            using var resp = await _http.SendAsync(req, ct);

            var json = await resp.Content.ReadAsStringAsync(ct);
            // 如果服务端会返回 4xx/5xx + JSON 错误体，先别急着 EnsureSuccessStatusCode，以便保留服务端错误信息
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"Upload failed: {(int)resp.StatusCode} {json}");

            return JsonSerializer.Deserialize<ApiResp<UploadAttachmentResult>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new ApiResp<UploadAttachmentResult>();
        }


        public async Task<ApiResp<string>> GetPreviewUrlAsync(string attachmentUrl, long? expires = null, CancellationToken ct = default)
        {
            var baseUrl = BuildFullUrl(_http.BaseAddress, _previewImagePath);

            // 组装 query
            var qb = HttpUtility.ParseQueryString(string.Empty);
            qb["attachmentUrl"] = attachmentUrl;                    // 必填
            if (expires.HasValue) qb["expires"] = expires.Value.ToString(); // 可选（秒）

            var url = $"{baseUrl}?{qb}";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp = await _http.SendAsync(req, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);

            return JsonSerializer.Deserialize<ApiResp<string>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            }) ?? new ApiResp<string>();
        }

       
    }



}