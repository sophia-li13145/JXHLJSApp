namespace JXHLJSApp.Models.Warehouse;

public sealed class WarehouseAreaDto
{
    public string? warehouseAreaNo { get; set; }
    public string? warehouseLocationId { get; set; }
    public string? id { get; set; }
    public string? location { get; set; }
    public string? code { get; set; }
    public string? name { get; set; }
    public string? label { get; set; }
    public string? value { get; set; }

    public string? selectedLocation => FirstNonEmpty(warehouseAreaNo, location, name, label, code, value);
    public string? selectedLocationId => FirstNonEmpty(warehouseLocationId, id, value, code);
    public string displayName => FirstNonEmpty(selectedLocation, selectedLocationId, "--") ?? "--";

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
