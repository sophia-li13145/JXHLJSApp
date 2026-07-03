namespace JXHLJSApp.Models.Warehouse;

public sealed class DictGroupDto
{
    public string? field { get; set; }
    public List<DictItemDto>? dictItems { get; set; }
}

public sealed class DictItemDto
{
    public string? dictItemValue { get; set; }
    public string? dictItemName { get; set; }
}
