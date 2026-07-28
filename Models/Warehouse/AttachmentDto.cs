using System.Text.Json.Serialization;

namespace JXHLJSApp.Models.Warehouse;

public sealed class AttachmentDto
{
    public string? attachmentExt { get; set; }
    public string? attachmentFolder { get; set; }
    public string? attachmentLocation { get; set; }
    public string? attachmentName { get; set; }
    public string? attachmentRealName { get; set; }
    public decimal? attachmentSize { get; set; }
    public string? attachmentUrl { get; set; }
    public string? createdTime { get; set; }
    public string? id { get; set; }
    public string? memo { get; set; }

    // 页面加载详情后通过附件预览接口填充，不参与后端 JSON 数据传输。
    [JsonIgnore]
    public ImageSource? previewImage { get; set; }
}
