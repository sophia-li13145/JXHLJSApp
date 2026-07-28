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
    public string? previewUrl { get; set; }
    [JsonIgnore]
    public bool hasPreview => !string.IsNullOrWhiteSpace(previewUrl);
    [JsonIgnore]
    public string displayName => FirstNonEmpty(attachmentName, attachmentRealName, "图片附件");

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "图片附件";
}
