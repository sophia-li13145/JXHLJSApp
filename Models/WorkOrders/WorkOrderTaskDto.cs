namespace JXHLJSApp.Models.WorkOrders;

public sealed class WorkOrderTaskDto
{
    public string? deviceCode { get; set; }
    public string? deviceName { get; set; }
    public string? id { get; set; }
    public string? inputSpecification { get; set; }
    public string? machineNo { get; set; }
    public string? operationCode { get; set; }
    public string? operationName { get; set; }
    public decimal? plannedQuantity { get; set; }
    public decimal? plannedWeight { get; set; }
    public string? targetSpecification { get; set; }
    public string? workOrderNo { get; set; }
    public string? workOrderStatus { get; set; }
}
