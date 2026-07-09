namespace JXHLJSApp.Models.Warehouse;

public sealed class WarehouseInfoDto
{
    public string? id { get; set; }
    public string? location { get; set; }
    public string? warehouseCode { get; set; }
    public string? warehouseName { get; set; }
    public string? dictItemValue { get; set; }
    public string? dictItemName { get; set; }
    public string? value { get; set; }
    public string? label { get; set; }
    public string? code { get; set; }
    public string? name { get; set; }

    public string? selectedCode => FirstNonEmpty(warehouseCode, dictItemValue, value, code, id);
    public string? selectedName => FirstNonEmpty(warehouseName, dictItemName, label, name, location);
    public string displayName => FirstNonEmpty(selectedName, selectedCode, "--") ?? "--";

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
