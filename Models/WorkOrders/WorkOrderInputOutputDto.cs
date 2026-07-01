namespace JXHLJSApp.Models.WorkOrders;

public sealed class WorkOrderInputOutputDto
{
    public string? id { get; set; }
    public string? workOrderNo { get; set; }
    public string? workOrderStatus { get; set; }
    public string? machineNo { get; set; }
    public string? machineType { get; set; }
    public string? deviceName { get; set; }
    public string? operationName { get; set; }
    public string? productSpecification { get; set; }
    public string? targetSpecification { get; set; }
    public string? moldSequence { get; set; }
    public decimal? completedWeight { get; set; }
    public decimal? productionWeight { get; set; }
    public decimal? plannedWeight { get; set; }
    public decimal? completedQuantity { get; set; }
    public decimal? productionQuantity { get; set; }
    public decimal? plannedQuantity { get; set; }

    public string machineDisplay => FirstNonEmpty(machineNo, machineType, deviceName);
    public string specificationDisplay => FirstNonEmpty(productSpecification, targetSpecification);
    public string completedWeightDisplay => FormatProgress(completedWeight, productionWeight ?? plannedWeight, "吨");
    public string completedQuantityDisplay => FormatProgress(completedQuantity, productionQuantity ?? plannedQuantity, "件");
    public string moldSequenceDisplay => string.IsNullOrWhiteSpace(moldSequence) ? "--" : moldSequence;

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "--";
    }

    private static string FormatProgress(decimal? current, decimal? total, string unit)
    {
        var currentText = FormatDecimal(current);
        var totalText = FormatDecimal(total);
        return total.HasValue ? $"{currentText}{unit} / {totalText}{unit}" : $"{currentText}{unit}";
    }

    private static string FormatDecimal(decimal? value)
    {
        return value.HasValue ? value.Value.ToString("0.##") : "--";
    }
}
