namespace JXHLJSApp.Models.WorkOrders;

public sealed class WorkOrderTaskDto
{
    public string? deviceCode { get; set; }
    public string? deviceName { get; set; }
    public string? id { get; set; }
    public string? inputSpecification { get; set; }
    public string? machineNo { get; set; }
    public string? materialName { get; set; }
    public string? operationCode { get; set; }
    public string? operationName { get; set; }
    public decimal? plannedQuantity { get; set; }
    public decimal? plannedWeight { get; set; }
    public string? targetSpecification { get; set; }
    public string? workOrderNo { get; set; }
    public string? workOrderStatus { get; set; }

    public string inputMaterialDisplay => JoinNonEmpty(materialName, inputSpecification);
    public string targetMaterialDisplay => JoinNonEmpty(materialName, targetSpecification);
    public string plannedWeightDisplay => plannedWeight.HasValue ? $"{plannedWeight.Value:0.##} 吨" : "--";

    private static string JoinNonEmpty(params string?[] values)
    {
        var text = string.Join(" ", values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));
        return string.IsNullOrWhiteSpace(text) ? "--" : text;
    }
}
