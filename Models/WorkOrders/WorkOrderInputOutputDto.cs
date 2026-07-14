namespace JXHLJSApp.Models.WorkOrders;

public sealed class WorkOrderInputOutputDto
{
    public string? id { get; set; }
    public string? workOrderNo { get; set; }
    public string? workOrderStatus { get; set; }
    public string? machineNo { get; set; }
    public string? customerCode { get; set; }
    public string? inputMaterialCode { get; set; }
    public string? inputMaterialName { get; set; }
    public string? inputOriginPlace { get; set; }
    public string? inputSpecification { get; set; }
    public string? inputSteel { get; set; }
    public string? inputSteelGrade { get; set; }
    public string? outputMaterialCode { get; set; }
    public string? outputMaterialName { get; set; }
    public string? outputOriginPlace { get; set; }
    public string? outputSpecification { get; set; }
    public string? outputSteel { get; set; }
    public string? outputSteelGrade { get; set; }
    public string? processName { get; set; }
    public string? unit { get; set; }
    public decimal? pieceWeight { get; set; }
    public string? wireTakeUpLength { get; set; }
    public decimal? currentSequenceNo { get; set; }
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
    public string inputMaterialDisplay => FirstNonEmpty(inputMaterialCode, inputMaterialName);
    public string outputMaterialDisplay => FirstNonEmpty(outputMaterialCode, outputMaterialName);

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
