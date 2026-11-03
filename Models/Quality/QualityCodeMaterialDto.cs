namespace JXHLJSApp.Models.Quality;

public sealed class QualityCodeMaterialDto
{
    public string? bizBatchNo { get; set; }
    public decimal? coilWeight { get; set; }
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
}
