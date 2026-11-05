using System.Text.Json.Serialization;

namespace JXHLJSApp.Models.Warehouse;

/// <summary>
/// /pda/rawMaterialReceiving/scanIncoming 接口的原始识别结果。
/// 数值字段保持为可空数值类型，再由页面模型转换为可编辑文本。
/// </summary>
public sealed class OcrIncomingResultDto
{
    public string? barcode { get; set; }
    public string? brandNo { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? coilCount { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? coilDiameter { get; set; }
    public string? coilNo { get; set; }
    public string? companyName { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? confidence { get; set; }
    public string? furnaceNo { get; set; }
    public string? inspector { get; set; }
    public string? materialClass { get; set; }
    public string? materialName { get; set; }
    public string? ocrRawText { get; set; }
    public string? originPlace { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? pieceWeight { get; set; }
    public string? pieceWeightUnit { get; set; }
    public string? productName { get; set; }
    public string? productionDate { get; set; }
    public string? qrCode { get; set; }
    public string? shift { get; set; }
    public string? spec { get; set; }
    public string? standard { get; set; }
    public string? strength { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? weightKg { get; set; }
    public string? workshop { get; set; }
}
