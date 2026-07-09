namespace JXHLJSApp.Models.Warehouse;

public sealed class SaveOcrIncomingImageRequestDto
{
    public int? coilCount { get; set; }
    public decimal? coilDiameter { get; set; }
    public AttachmentDto? fileInfo { get; set; }
    public string? furnaceNo { get; set; }
    public string? instockNo { get; set; }
    public string? materialClass { get; set; }
    public string? materialName { get; set; }
    public string? materialType { get; set; }
    public string? originPlace { get; set; }
    public decimal? pieceWeight { get; set; }
    public string? spec { get; set; }
    public string? strength { get; set; }
}
