namespace JXHLJSApp.Models.Warehouse;

public sealed class PackagingSaveRequestDto
{
    public decimal? actualWeight { get; set; }
    public decimal? length { get; set; }
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    public string? originPlace { get; set; }
    public decimal? pieceWeight { get; set; }
    public string? qrCode { get; set; }
    public string? specification { get; set; }
    public string? steelGrade { get; set; }
    public string? workOrderNo { get; set; }
}
