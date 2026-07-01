namespace JXHLJSApp.Models.WorkOrders;

public sealed class WorkOrderDictDto
{
    public string? field { get; set; }
    public List<WorkOrderDictItemDto>? dictItems { get; set; }
}

public sealed class WorkOrderDictItemDto
{
    public string? dictItemValue { get; set; }
    public string? dictItemName { get; set; }
}
