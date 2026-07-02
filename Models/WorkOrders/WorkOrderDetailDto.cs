namespace JXHLJSApp.Models.WorkOrders;

public sealed class WorkOrderDetailDto
{
    public string? billetLowerTolerance { get; set; }
    public string? billetUpperTolerance { get; set; }
    public string? coilDiameterControl { get; set; }
    public string? coilWeightRequirement { get; set; }
    public string? deviceCode { get; set; }
    public string? deviceName { get; set; }
    public string? drawMode { get; set; }
    public string? dvSpeed { get; set; }
    public string? id { get; set; }
    public string? intermediateSpecification { get; set; }
    public string? machineNo { get; set; }
    public string? machineType { get; set; }
    public string? materialProperty { get; set; }
    public string? memo { get; set; }
    public List<WorkOrderMoldSequenceDto>? moldSequenceList { get; set; }
    public bool? needBending { get; set; }
    public bool? needPackagingCloth { get; set; }
    public bool? needPalletizing { get; set; }
    public bool? needPhosphating { get; set; }
    public string? operationCode { get; set; }
    public string? operationName { get; set; }
    public string? otherRequirement { get; set; }
    public string? ovalityControl { get; set; }
    public string? packageMethod { get; set; }
    public string? packageWeight { get; set; }
    public string? packagingClothColor { get; set; }
    public string? pitchControl { get; set; }
    public string? productSpecification { get; set; }
    public string? saleMode { get; set; }
    public string? steelGrade { get; set; }
    public string? wireTakeUpLength { get; set; }
    public string? wireTakeUpMode { get; set; }
    public string? wireTakeUpSpeed { get; set; }
    public string? workOrderNo { get; set; }
    public string? workOrderStatus { get; set; }

    public string machineDisplay => FirstNonEmpty(machineNo, machineType, deviceName);
    public string specificationDisplay => FirstNonEmpty(productSpecification, intermediateSpecification);
    public string completedWeightDisplay => FormatDecimal(moldSequenceList?.Sum(item => item.productionWeight), "吨");
    public string completedQuantityDisplay => FormatDecimal(moldSequenceList?.Sum(item => item.productionQuantity), "件");
    public string moldSequenceDisplay => moldSequenceList is { Count: > 0 }
        ? string.Join("；", moldSequenceList.Select(item => $"{FormatDecimal(item.moldSequence, string.Empty)}# {FormatDecimal(item.pieceWeight, "KG")}/{FormatDecimal(item.productionQuantity, "件")}/{FormatDecimal(item.productionWeight, "吨")}"))
        : "--";

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "--";
    }

    private static string FormatDecimal(decimal? value, string unit)
    {
        var text = value.HasValue ? value.Value.ToString("0.##") : "--";
        return string.IsNullOrWhiteSpace(unit) || text == "--" ? text : $"{text}{unit}";
    }
}

public sealed class WorkOrderMoldSequenceDto
{
    public decimal? moldSequence { get; set; }
    public decimal? pieceWeight { get; set; }
    public decimal? productionQuantity { get; set; }
    public decimal? productionWeight { get; set; }
}
