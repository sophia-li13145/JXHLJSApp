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
    public string machineDisplay => FirstNonEmpty(machineNo, deviceName, deviceCode);
    public string operationDisplay => FirstNonEmpty(operationName, operationCode);
    public string plannedWeightDisplay => FormatDecimal(plannedWeight, "吨");
    public string productionWeightDisplay => plannedWeight.HasValue ? plannedWeightDisplay : FormatDecimal(plannedQuantity, "");
    public string statusBackgroundColor => IsCompletedStatus ? "#EAFBF1" : IsRunningStatus ? "#EAF3FF" : "#F4F6FA";
    public string statusTextColor => IsCompletedStatus ? "#009B57" : IsRunningStatus ? "#006BFF" : "#001431";

    private bool IsCompletedStatus => ContainsStatus("完成") || ContainsStatus("completed") || ContainsStatus("finish");
    private bool IsRunningStatus => ContainsStatus("执行") || ContainsStatus("running") || ContainsStatus("start");

    private bool ContainsStatus(string value) => !string.IsNullOrWhiteSpace(workOrderStatus)
        && workOrderStatus.Contains(value, StringComparison.OrdinalIgnoreCase);

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "--";
    }

    private static string FormatDecimal(decimal? value, string unit)
    {
        var text = value.HasValue ? value.Value.ToString("0.##") : "--";
        return string.IsNullOrWhiteSpace(unit) || text == "--" ? text : $"{text}{unit}";
    }

    private static string JoinNonEmpty(params string?[] values)
    {
        var text = string.Join(" ", values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));
        return string.IsNullOrWhiteSpace(text) ? "--" : text;
    }
}
