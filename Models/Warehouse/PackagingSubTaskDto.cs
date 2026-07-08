namespace JXHLJSApp.Models.Warehouse;

public sealed class PackagingSubTaskDto
{
    public string? id { get; set; }
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    public string? operationCode { get; set; }
    public string? operationName { get; set; }
    public string? operationTaskNo { get; set; }
    public string? workOrderId { get; set; }
    public string? workOrderNo { get; set; }
    public string? workOrderStatus { get; set; }

    public string taskNoDisplay => string.IsNullOrWhiteSpace(operationTaskNo) ? "--" : operationTaskNo!;
    public string workOrderNoDisplay => string.IsNullOrWhiteSpace(workOrderNo) ? "--" : workOrderNo!;
    public string materialDisplay => string.IsNullOrWhiteSpace(materialName) ? "--" : materialName!;
    public string statusDisplay => string.IsNullOrWhiteSpace(workOrderStatus) ? "待包装" : workOrderStatus!;
}
