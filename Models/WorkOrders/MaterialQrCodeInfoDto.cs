namespace JXHLJSApp.Models.WorkOrders;

public sealed class MaterialQrCodeInfoDto
{
    public string? bizBatchNo { get; set; }
    public string? stockBatch { get; set; }
    public string? coilWeight { get; set; }
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    public string? materialState { get; set; }
    public string? materialStateName { get; set; }
    public string? qrCode { get; set; }
    public string? qrStatus { get; set; }
    public string? qrStatusName { get; set; }
    public int? qrTimes { get; set; }
    public string? sourceBizType { get; set; }
    public string? sourceBizTypeName { get; set; }
    public string? spec { get; set; }
    public string? weightUnit { get; set; }
    public decimal? length { get; set; }
    public string? lengthUnit { get; set; }
}
