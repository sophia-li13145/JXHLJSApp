namespace JXHLJSApp.Models.Warehouse;

public sealed class EditRawMaterialStashDetailRequestDto
{
    public int? coilCount { get; set; }
    public decimal? coilDiameter { get; set; }
    public string? furnaceNo { get; set; }
    public string? instockNo { get; set; }
    public decimal? instockQty { get; set; }
    public string? origin { get; set; }
    public string? qrCode { get; set; }
    public string? spec { get; set; }
    public string? strength { get; set; }
}
