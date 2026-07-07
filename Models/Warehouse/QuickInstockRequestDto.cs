namespace JXHLJSApp.Models.Warehouse;

public sealed class QuickInstockRequestDto
{
    public List<QuickInstockDetailDto> detailList { get; set; } = new();
}

public sealed class QuickInstockDetailDto
{
    public int? coilCount { get; set; }
    public decimal? coilDiameter { get; set; }
    public int? count { get; set; }
    public int? countSeq { get; set; }
    public string? furnaceNo { get; set; }
    public string? instockNo { get; set; }
    public decimal? instockQty { get; set; }
    public string? instockWarehouse { get; set; }
    public string? instockWarehouseCode { get; set; }
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    public string? memo { get; set; }
    public string? origin { get; set; }
    public string? qrCode { get; set; }
    public string? spec { get; set; }
    public string? strength { get; set; }
    public string? unit { get; set; }
}
