namespace JXHLJSApp.Models.Warehouse;

public sealed class WarehouseInfoDto
{
    public string? id { get; set; }
    public string? location { get; set; }
    public string? warehouseCode { get; set; }
    public string? warehouseName { get; set; }

    public string displayName => string.IsNullOrWhiteSpace(warehouseName) ? "--" : warehouseName;
}
