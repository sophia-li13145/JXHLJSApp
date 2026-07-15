namespace JXHLJSApp.Models.WorkOrders;

public sealed class WorkOrderDetailDto
{
    public decimal? actualQuantity { get; set; }
    public decimal? actualWeight { get; set; }
    public string? billetLowerTolerance { get; set; }
    public string? billetUpperTolerance { get; set; }
    public string? blankSpecification { get; set; }
    public string? coilDiameterControl { get; set; }
    public string? coilWeightRequirement { get; set; }
    public string? deviceCode { get; set; }
    public string? deviceName { get; set; }
    public string? drawMode { get; set; }
    public string? dvSpeed { get; set; }
    public string? furnaceNo { get; set; }
    public string? id { get; set; }
    public string? inputSpecification { get; set; }
    public string? inputSteelGrade { get; set; }
    public string? inspectionSchemeCode { get; set; }
    public string? inspectionSchemeName { get; set; }
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
    public decimal? plannedQuantity { get; set; }
    public decimal? plannedWeight { get; set; }
    public string? productSpecification { get; set; }
    public decimal? productionQuantity { get; set; }
    public decimal? productionWeight { get; set; }
    public string? qualityNo { get; set; }
    public string? saleMode { get; set; }
    public string? steelGrade { get; set; }
    public string? usagePurpose { get; set; }
    public string? wireTakeUpLength { get; set; }
    public string? wireTakeUpMode { get; set; }
    public string? wireTakeUpSpeed { get; set; }
    public string? workOrderNo { get; set; }
    public string? workOrderStatus { get; set; }

    public string machineDisplay => FirstNonEmpty(machineNo, machineType, deviceName);
    public string specificationDisplay => FirstNonEmpty(productSpecification, intermediateSpecification);
    public string steelGradeSpecificationDisplay => JoinNonEmpty(steelGrade, FirstNonEmptyOrDefault(productSpecification, intermediateSpecification));
    public string completedWeightDisplay => FormatProgress(actualWeight, plannedWeight, "吨");
    public string completedQuantityDisplay => FormatProgress(actualQuantity, plannedQuantity, "件");
    public string statusBackgroundColor => IsPausedStatus ? "#FFF8E8" : "#F2FFF8";
    public string statusBorderColor => IsPausedStatus ? "#F5A623" : "#18C77A";
    public string statusTextColor => IsPausedStatus ? "#C47A00" : "#00A651";
    public string statusSeparatorColor => IsPausedStatus ? "#F3D9A6" : "#CDEEDC";
    public string moldSequenceDisplay => moldSequenceList is { Count: > 0 }
        ? string.Join("-", moldSequenceList.Select(item => item.moldSequence))
        : "--";

    private bool IsPausedStatus => !string.IsNullOrWhiteSpace(workOrderStatus)
        && (workOrderStatus.Contains("暂停", StringComparison.OrdinalIgnoreCase)
            || workOrderStatus.Contains("stop", StringComparison.OrdinalIgnoreCase)
            || workOrderStatus.Contains("pause", StringComparison.OrdinalIgnoreCase));

    private static string FirstNonEmpty(params string?[] values)
    {
        return FirstNonEmptyOrDefault(values) ?? "--";
    }

    private static string? FirstNonEmptyOrDefault(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string JoinNonEmpty(params string?[] values)
    {
        var text = string.Join(" ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(text) ? "--" : text;
    }

    private static string FormatDecimal(decimal? value, string unit)
    {
        var text = value.HasValue ? value.Value.ToString("0.##") : "--";
        return string.IsNullOrWhiteSpace(unit) || text == "--" ? text : $"{text}{unit}";
    }

    private static string FormatProgress(decimal? actualValue, decimal? plannedValue, string unit)
    {
        return $"{FormatDecimal(actualValue, unit)} / {FormatDecimal(plannedValue, unit)}";
    }
}

public sealed class WorkOrderMoldSequenceDto
{
    public string? moldSequence { get; set; }
    public decimal? pieceWeight { get; set; }
    public decimal? productionQuantity { get; set; }
    public decimal? productionWeight { get; set; }
}
