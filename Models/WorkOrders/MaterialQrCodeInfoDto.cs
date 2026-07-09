using System.Text.Json.Serialization;
using JXHLJSApp.Models.Warehouse;

namespace JXHLJSApp.Models.WorkOrders;

public sealed class MaterialQrCodeInfoDto
{
    public string? bizBatchNo { get; set; }
    public string? stockBatch { get; set; }
    public string? coilWeight { get; set; }
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    public string? operationCode { get; set; }
    public string? operationName { get; set; }
    public decimal? outputCount { get; set; }
    public string? workOrderNo { get; set; }
    public string? materialState { get; set; }
    public string? materialStateName { get; set; }
    public string? materialType { get; set; }
    public string? originPlace { get; set; }
    public string? specification { get; set; }
    public string? steelGrade { get; set; }
    public string? unit { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? weight { get; set; }
    public string? qrCode { get; set; }
    public string? qrStatus { get; set; }
    public string? qrStatusName { get; set; }
    public decimal? qrTimes { get; set; }
    public string? sourceBizType { get; set; }
    public string? sourceBizTypeName { get; set; }
    public string? spec { get; set; }
    public string? weightUnit { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? length { get; set; }
    public string? lengthUnit { get; set; }
}
