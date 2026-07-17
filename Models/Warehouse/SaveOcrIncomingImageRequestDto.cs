namespace JXHLJSApp.Models.Warehouse;

public sealed class SaveOcrIncomingImageRequestDto
{
    public int? coilCount { get; set; }
    public decimal? coilDiameter { get; set; }
    public string? coilNo { get; set; }
    public string? companyName { get; set; }
    public decimal? confidence { get; set; }
    public AttachmentDto? fileInfo { get; set; }
    public string? furnaceNo { get; set; }
    public string? instockNo { get; set; }
    public string? materialClass { get; set; }
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    public string? materialType { get; set; }
    public string? ocrRawText { get; set; }
    public string? originPlace { get; set; }
    public decimal? pieceWeight { get; set; }
    public string? pieceWeightUnit { get; set; }
    public string? productName { get; set; }
    public string? productionDate { get; set; }
    public string? qrCode { get; set; }
    public string? spec { get; set; }
    public string? standard { get; set; }
    public string? strength { get; set; }
    public string? workshop { get; set; }
}
