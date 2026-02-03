using JXHLJSApp.Services.Common;
using JXHLJSApp.Models;
using JXHLJSApp.Tools;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace JXHLJSApp.Services
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
        Task<ApiResp<bool>> DeleteAttachmentAsync(string id, string atturl, CancellationToken ct = default);
    }
}
