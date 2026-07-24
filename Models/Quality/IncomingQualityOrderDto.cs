namespace JXHLJSApp.Models.Quality;

public sealed class IncomingQualityOrderDto
{
    public int? done { get; set; }
    public string? incomingQualityNo { get; set; }
    public string? instockNo { get; set; }
    public string? materialNames { get; set; }
    public string? status { get; set; }
    public string? statusName { get; set; }
    public int? total { get; set; }
    public string? delStatus { get; set; }
    public string? delStatusName { get; set; }
    public string? materialName { get; set; }
    public string? materialCode { get; set; }
    public string? specification { get; set; }

    public string incomingQualityNoDisplay => string.IsNullOrWhiteSpace(incomingQualityNo) ? "未生成来料质检单" : incomingQualityNo!;
    public string instockNoDisplay => string.IsNullOrWhiteSpace(instockNo) ? "-" : instockNo!;
    public string statusDisplay => FirstNonEmpty(statusName, delStatusName, MapStatus(status), MapStatus(delStatus), "未提交");
    public string statusBackground => statusDisplay switch
    {
        "待质检" => "#FFF6E8",
        "检验完成" or "已完成" => "#E9FBEF",
        "未提交" => "#F1F3F7",
        _ => "#EEF3FA"
    };
    public string statusColor => statusDisplay switch
    {
        "待质检" => "#D97706",
        "检验完成" or "已完成" => "#16A34A",
        "未提交" => "#4B5563",
        _ => "#244B88"
    };
    public string totalDisplay => total.HasValue ? $"{total}件" : "-";
    public string doneDisplay => done.HasValue ? $"已扫记录：{done} 条" : "已扫记录：-";
    public string pendingStorageText => "等待仓储提交入库单明细...";
    public bool isUnsubmitted => IsStatus("0", "UNSUBMITTED", "unsubmitted", "未提交");
    public bool isInspectionStarted => !isUnsubmitted;
    public bool canDelete => isUnsubmitted;
    public string materialDisplay
    {
        get
        {
            var text = !string.IsNullOrWhiteSpace(materialNames)
                ? materialNames
                : string.Join(" ", new[] { materialName, materialCode, specification }.Where(v => !string.IsNullOrWhiteSpace(v)));
            return string.IsNullOrWhiteSpace(text) ? "-" : text;
        }
    }
    public bool hasMaterial => !string.IsNullOrWhiteSpace(materialDisplay) && materialDisplay != "-";

    private bool IsStatus(params string[] values) => values.Any(value => string.Equals(status, value, StringComparison.OrdinalIgnoreCase) || string.Equals(delStatus, value, StringComparison.OrdinalIgnoreCase) || string.Equals(statusDisplay, value, StringComparison.OrdinalIgnoreCase));
    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
    private static string? MapStatus(string? value) => value?.ToLowerInvariant() switch
    {
        "0" or "unsubmitted" => "未提交",
        "1" or "wait_inspection" => "待质检",
        "2" or "completed" => "检验完成",
        _ => value
    };
}

public sealed class IncomingQualityOrderDetailDto
{
    public List<IncomingQualityScanDetailDto>? details { get; set; }
    public string? incomingQualityNo { get; set; }
    public string? instockNo { get; set; }
    public List<IncomingQualityMaterialDetailDto>? materialDetailList { get; set; }
    public int? total { get; set; }
    public string? status { get; set; }
    public string? statusName { get; set; }
    public string? delStatus { get; set; }
    public string? delStatusName { get; set; }

    public string incomingQualityNoDisplay => string.IsNullOrWhiteSpace(incomingQualityNo) ? "-" : incomingQualityNo!;
    public string instockNoDisplay => string.IsNullOrWhiteSpace(instockNo) ? "-" : instockNo!;
    public string totalDisplay => total.HasValue ? $"{total} 件" : "-";
    public IReadOnlyList<IncomingQualityMaterialDetailDto> materialDetails => materialDetailList ?? new List<IncomingQualityMaterialDetailDto>();
    public int? materialDetailCount => materialDetails.Count;
    public string statusDisplay => FirstNonEmpty(statusName, delStatusName, MapStatus(status), MapStatus(delStatus), "未提交");
    public IReadOnlyList<IncomingQualityScanDetailDto> scanDetails => details ?? new List<IncomingQualityScanDetailDto>();
    public int? done { get; set; }
    public int? scanCount => done ?? scanDetails.Count;
    public bool isUnsubmitted => IsStatus("0", "UNSUBMITTED", "unsubmitted", "未提交");
    public bool isWaitInspection => IsStatus("1", "WAIT_INSPECTION", "wait_inspection", "待质检");
    public bool isCompleted => IsStatus("2", "COMPLETED", "completed", "已完成", "检验完成");

    private bool IsStatus(params string[] values) => values.Any(value => string.Equals(status, value, StringComparison.OrdinalIgnoreCase) || string.Equals(delStatus, value, StringComparison.OrdinalIgnoreCase) || string.Equals(statusDisplay, value, StringComparison.OrdinalIgnoreCase));
    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
    private static string? MapStatus(string? value) => value?.ToLowerInvariant() switch
    {
        "0" or "unsubmitted" => "未提交",
        "1" or "wait_inspection" => "待质检",
        "2" or "completed" => "检验完成",
        _ => value
    };
}

public sealed class IncomingQualityMaterialDetailDto
{
    public int? count { get; set; }
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    public string? spec { get; set; }

    public string countDisplay => count.HasValue ? $"{count} 件" : "-";
    public string materialSpecDisplay => $"物料 {FirstNonEmpty(materialName, "-")}  |  规格 {FirstNonEmpty(spec, "-")}";
    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}

public sealed class IncomingQualityScanDetailDto
{
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    public string? otherProblemItem { get; set; }
    public string? problemPoint { get; set; }
    public string? problemPointName { get; set; }
    public string? qrCode { get; set; }
    public string? qrCodeNo { get; set; }
    public string? spec { get; set; }
    public string? steelGrade { get; set; }
    public string? inspectResult { get; set; }
    public string? inspectResultName { get; set; }
    public string problemPointDisplay => FirstNonEmpty(otherProblemItem, problemPointName, problemPoint, "-");
    public string qrCodeDisplay => string.IsNullOrWhiteSpace(qrCode) ? (string.IsNullOrWhiteSpace(qrCodeNo) ? "-" : qrCodeNo!) : qrCode!;
    public string materialSpecDisplay => $"物料 {BuildMaterialName()}  规格 {FirstNonEmpty(spec, "-")}";
    public string inspectResultDisplay => FirstNonEmpty(inspectResultName, inspectResult, HasProblemDescription ? "不合格" : "-");
    public Color inspectResultColor => inspectResultDisplay.Contains("合格") && !inspectResultDisplay.Contains("不合格") ? Color.FromArgb("#00A86B") : Color.FromArgb("#FF4D5E");
    private string BuildMaterialName()
    {
        var name = FirstNonEmpty(materialName, "-");
        return string.IsNullOrWhiteSpace(steelGrade) ? name : $"{name} {steelGrade}";
    }

    private bool HasProblemDescription => !string.IsNullOrWhiteSpace(otherProblemItem) || !string.IsNullOrWhiteSpace(problemPoint);
    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}

public sealed class IncomingQualitySaveResultRequestDto
{
    public string? inspectResult { get; set; }
    public string? instockNo { get; set; }
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    public string? otherExceptionDesc { get; set; }
    public string? otherProblemItem { get; set; }
    public string? problemPoint { get; set; }
    public string? qrCode { get; set; }
}

public sealed class IncomingQualityScanMaterialDto
{
    public string? furnaceNo { get; set; }
    public string? instockNo { get; set; }
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    public string? origin { get; set; }
    public string? qrCode { get; set; }
    public string? qrCodeNo { get; set; }
    public string? spec { get; set; }

    public string qrCodeDisplay => string.IsNullOrWhiteSpace(qrCode) ? (string.IsNullOrWhiteSpace(qrCodeNo) ? "-" : qrCodeNo!) : qrCode!;
    public string materialDisplay
    {
        get
        {
            var parts = new[] { materialName, materialCode, spec }.Where(v => !string.IsNullOrWhiteSpace(v));
            var text = string.Join(" ", parts);
            return string.IsNullOrWhiteSpace(text) ? "-" : text;
        }
    }
}



public sealed class ProductionQualityScanMaterialRequestDto
{
    public string? qrCode { get; set; }
    public string? qualityNo { get; set; }
    public string? workOrderNo { get; set; }
}

public sealed class ProductionQualityScanMaterialDto
{
    public string? acidRatio { get; set; }
    public string? actualDiameterMm { get; set; }
    public string? productionBatchNo { get; set; }
    public string? productionBatch { get; set; }
    public string? batchNo { get; set; }
    public string? businessType { get; set; }
    public long? childSeq { get; set; }
    public string? coilDiameterControl { get; set; }
    public string? coilPitchControl { get; set; }
    public string? customerCode { get; set; }
    public string? deviceCode { get; set; }
    public string? deviceName { get; set; }
    public string? elongationRate { get; set; }
    public string? freeAcid { get; set; }
    public string? freeAcidSampling { get; set; }
    public string? furnaceNo { get; set; }
    public string? hydrochloricAcidConcentration1 { get; set; }
    public string? hydrochloricAcidConcentration2 { get; set; }
    public string? inputDiameterMm { get; set; }
    public string? inputSpecification { get; set; }
    public string? inspectResult { get; set; }
    public string? inspectionSchemeCode { get; set; }
    public string? inspectionSchemeName { get; set; }
    public string? listing { get; set; }
    public string? lowerToleranceValue { get; set; }
    public string? machineNo { get; set; }
    public string? machine { get; set; }
    public string? productionDate { get; set; }
    public string? prodcutDiameter { get; set; }
    public string? workOrderRingDiameter { get; set; }
    public string? workOrderCoilDiameterControl { get; set; }
    public string? workOrderCoilPitchControl { get; set; }
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    public string? memo { get; set; }
    public string? originPlace { get; set; }
    public string? phosphatingTemperature { get; set; }
    public string? pieceNo { get; set; }
    public string? plateNo { get; set; }
    public string? productDiameter { get; set; }
    public string? qrCode { get; set; }
    public int? qrTimes { get; set; }
    public string? qualityMaterialId { get; set; }
    public bool resultSaved { get; set; }
    public string? saponificationPhValue { get; set; }
    public string? saponificationTemperature { get; set; }
    public string? shiftNo { get; set; }
    public string? shiftCode { get; set; }
    public string? shiftName { get; set; }
    public string? spec { get; set; }
    public string? spoolWeightRequirement { get; set; }
    public string? steelGrade { get; set; }
    public string? strengthMpa { get; set; }
    public string? surfaceCondition { get; set; }
    public string? targetSpecification { get; set; }
    public string? totalAcid { get; set; }
    public string? totalAcidSampling { get; set; }
    public string? upperToleranceValue { get; set; }
    public string? workOrderNo { get; set; }
}

public sealed class ProductionQualityCommitRequestDto
{
    public string? acidRatio { get; set; }
    public string? actualDiameterMm { get; set; }
    public string? coilDiameterControl { get; set; }
    public string? coilPitchControl { get; set; }
    public string? elongationRate { get; set; }
    public string? freeAcid { get; set; }
    public string? freeAcidSampling { get; set; }
    public string? hydrochloricAcidConcentration1 { get; set; }
    public string? hydrochloricAcidConcentration2 { get; set; }
    public string? standardDiameterMm { get; set; }
    public string? brokenDiameterMm { get; set; }
    public string? sectionShrinkageRate { get; set; }
    public string? tensileStrengthMpa { get; set; }
    public string? twistCount { get; set; }
    public string? inspectResult { get; set; }
    public string? memo { get; set; }
    public string? phosphatingTemperature { get; set; }
    public string? qualityNo { get; set; }
    public string? recorder { get; set; }
    public string? saponificationPhValue { get; set; }
    public string? saponificationTemperature { get; set; }
    public string? strengthMpa { get; set; }
    public string? surfaceCondition { get; set; }
    public string? totalAcid { get; set; }
    public string? totalAcidSampling { get; set; }
    public string? workOrderNo { get; set; }
}





public sealed class ProductionManualInspectionSaveResultRequestDto
{
    public string? actualDiameterMm { get; set; }
    public string? coilDiameterControl { get; set; }
    public string? coilPitchControl { get; set; }
    public string? elongationRate { get; set; }
    public string? inspectResult { get; set; }
    public string? memo { get; set; }
    public string? qrCode { get; set; }
    public string? qualityMaterialId { get; set; }
    public string? qualityNo { get; set; }
    public string? strengthMpa { get; set; }
    public string? surfaceCondition { get; set; }
    public string? workOrderNo { get; set; }
}

public sealed class ProductionSamplingOrFullCommitRequestDto
{
    public string? actualDiameterMm { get; set; }
    public string? brokenDiameter { get; set; }
    public string? coilDiameterControl { get; set; }
    public string? coilPitchControl { get; set; }
    public string? elongationRate { get; set; }
    public string? inspectResult { get; set; }
    public string? memo { get; set; }
    public string? qrCode { get; set; }
    public string? qualityMaterialId { get; set; }
    public string? qualityNo { get; set; }
    public string? reductionOfAreaRate { get; set; }
    public string? strengthMpa { get; set; }
    public string? surfaceCondition { get; set; }
    public string? torsion { get; set; }
    public string? workOrderNo { get; set; }
}

public sealed class ProductionSamplingOrFullCompleteRequestDto
{
    public string? qualityNo { get; set; }
    public string? workOrderNo { get; set; }
}

public sealed class ProductionPicklingCommitRequestDto
{
    public string? acidRatio { get; set; }
    public string? freeAcid { get; set; }
    public string? freeAcidSampling { get; set; }
    public string? hydrochloricAcidConcentration1 { get; set; }
    public string? hydrochloricAcidConcentration2 { get; set; }
    public string? inspectDate { get; set; }
    public string? inspectResult { get; set; }
    public string? inspecter { get; set; }
    public string? memo { get; set; }
    public string? phosphatingTemperature { get; set; }
    public string? qualityNo { get; set; }
    public string? saponificationPhValue { get; set; }
    public string? saponificationTemperature { get; set; }
    public string? totalAcid { get; set; }
    public string? totalAcidSampling { get; set; }
    public string? workOrderNo { get; set; }
}

public sealed class ProductionFirstInspectionCommitRequestDto
{
    public string? actualDiameterMm { get; set; }
    public string? coilDiameterControl { get; set; }
    public string? coilPitchControl { get; set; }
    public string? elongationRate { get; set; }
    public string? inspectResult { get; set; }
    public string? memo { get; set; }
    public string? qualityNo { get; set; }
    public string? strengthMpa { get; set; }
    public string? surfaceCondition { get; set; }
    public string? workOrderNo { get; set; }
}

public sealed class QualityDictOption
{
    public string Value { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsSelected { get; set; }
}

public sealed class IncomingQualityStatusFilter
{
    public string Name { get; init; } = string.Empty;
    public string? Value { get; init; }
    public bool IsSelected { get; set; }
}


public sealed class ProductionQualityOrderDto
{
    public string? businessType { get; set; }
    public string? id { get; set; }
    public string? inspectStatus { get; set; }
    public string? inspectionSchemeCode { get; set; }
    public string? inspectionSchemeTypeName { get; set; }
    public string? inspectionSchemeName { get; set; }
    public string? machineNo { get; set; }
    public string? orderNumber { get; set; }
    public string? qualityNo { get; set; }
    public string? qualityType { get; set; }
    public string? qualityTypeName { get; set; }
    public string? resourceCode { get; set; }
    public string? resourceName { get; set; }

    public string titleDisplay => FirstNonEmpty(inspectionSchemeName, qualityTypeName, inspectionSchemeTypeName, "质检任务");
    public string machineDisplay => FirstNonEmpty(resourceName, machineNo, resourceCode, "-");
    public string statusDisplay => inspectStatus switch
    {
        "0" => "新建",
        "1" => "待检验",
        "2" => "检验中",
        "3" => "检验完成",
        _ => string.IsNullOrWhiteSpace(inspectStatus) ? "待检验" : inspectStatus!
    };
    public string statusBackground => statusDisplay switch
    {
        "待检验" => "#FFF9E8",
        "检验中" => "#EAF2FF",
        "检验完成" => "#E9FBEF",
        _ => "#EEF3FA"
    };
    public string statusColor => statusDisplay switch
    {
        "待检验" => "#D87500",
        "检验中" => "#1D4ED8",
        "检验完成" => "#16A34A",
        _ => "#244B88"
    };
    public string qualityNoDisplay => string.IsNullOrWhiteSpace(qualityNo) ? "-" : qualityNo!;
    public string orderNumberDisplay => string.IsNullOrWhiteSpace(orderNumber) ? "-" : orderNumber!;

    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}

public sealed class ProductionQualityDetailDto
{
    public string? acidRatio { get; set; }
    public string? actualDiameterMm { get; set; }
    public string? businessType { get; set; }
    public long? childSeq { get; set; }
    public string? coilDiameterControl { get; set; }
    public string? coilPitchControl { get; set; }
    public string? deviceCode { get; set; }
    public string? deviceName { get; set; }
    public string? elongationRate { get; set; }
    public string? freeAcid { get; set; }
    public string? freeAcidSampling { get; set; }
    public string? furnaceNo { get; set; }
    public string? hydrochloricAcidConcentration1 { get; set; }
    public string? hydrochloricAcidConcentration2 { get; set; }
    public string? standardDiameterMm { get; set; }
    public string? brokenDiameterMm { get; set; }
    public string? sectionShrinkageRate { get; set; }
    public string? tensileStrengthMpa { get; set; }
    public string? twistCount { get; set; }
    public string? productionBatchNo { get; set; }
    public string? productionBatch { get; set; }
    public string? batchNo { get; set; }
    public string? shiftNo { get; set; }
    public string? plateNo { get; set; }
    public string? customerCode { get; set; }
    public string? originPlace { get; set; }
    public string? machine { get; set; }
    public string? productionDate { get; set; }
    public string? prodcutDiameter { get; set; }
    public string? workOrderRingDiameter { get; set; }
    public string? workOrderCoilDiameterControl { get; set; }
    public string? workOrderCoilPitchControl { get; set; }
    public string? qrCode { get; set; }
    public string? qualityMaterialId { get; set; }
    public string? inputDiameterMm { get; set; }
    public string? inputSpecification { get; set; }
    public string? inspectResult { get; set; }
    public string? inspectionSchemeCode { get; set; }
    public string? inspectionSchemeName { get; set; }
    public string? inspectDate { get; set; }
    public string? inspectStatus { get; set; }
    public string? lowerToleranceValue { get; set; }
    public string? memo { get; set; }
    public string? phosphatingTemperature { get; set; }
    public string? pieceNo { get; set; }
    public string? productDiameter { get; set; }
    public string? saponificationPhValue { get; set; }
    public string? saponificationTemperature { get; set; }
    public string? spoolWeightRequirement { get; set; }
    public string? steelGrade { get; set; }
    public string? strengthMpa { get; set; }
    public string? surfaceCondition { get; set; }
    public string? targetSpecification { get; set; }
    public string? totalAcid { get; set; }
    public string? totalAcidSampling { get; set; }
    public string? upperToleranceValue { get; set; }
    public string? shiftCode { get; set; }
    public string? shiftName { get; set; }
    public string? workOrderStatus { get; set; }
    public string? workOrderNo { get; set; }
    public string? qualityNo { get; set; }
    public string? qualityType { get; set; }
    public string? qualityTypeName { get; set; }
    public string? processCode { get; set; }
    public string? processName { get; set; }
    public string? inspectionSchemeTypeName { get; set; }
    public List<ProductionQualityInspectionItemDto>? inspectionItemList { get; set; }
    public List<ProductionQualityMaterialDto>? materialList { get; set; }
}

public sealed class ProductionQualityInspectionItemDto
{
    public string? inspectionMode { get; set; }
    public string? inspectionStandard { get; set; }
    public string? itemCode { get; set; }
    public string? itemName { get; set; }
    public string? lowerLimit { get; set; }
    public string? standardValue { get; set; }
    public string? unit { get; set; }
    public string? upperLimit { get; set; }
}

public sealed class ProductionQualityMaterialDto
{
    public string? acidRatio { get; set; }
    public string? actualDiameterMm { get; set; }
    public string? productionBatchNo { get; set; }
    public string? productionBatch { get; set; }
    public string? batchNo { get; set; }
    public string? businessType { get; set; }
    public long? childSeq { get; set; }
    public string? coilDiameterControl { get; set; }
    public string? coilPitchControl { get; set; }
    public string? deviceCode { get; set; }
    public string? deviceName { get; set; }
    public string? elongationRate { get; set; }
    public string? freeAcid { get; set; }
    public string? freeAcidSampling { get; set; }
    public string? furnaceNo { get; set; }
    public string? hydrochloricAcidConcentration1 { get; set; }
    public string? hydrochloricAcidConcentration2 { get; set; }
    public string? inputDiameterMm { get; set; }
    public string? inputSpecification { get; set; }
    public string? inspectResult { get; set; }
    public string? inspectionSchemeCode { get; set; }
    public string? inspectionSchemeName { get; set; }
    public string? listing { get; set; }
    public string? lowerToleranceValue { get; set; }
    public string? machineNo { get; set; }
    public string? machine { get; set; }
    public string? productionDate { get; set; }
    public string? prodcutDiameter { get; set; }
    public string? workOrderRingDiameter { get; set; }
    public string? workOrderCoilDiameterControl { get; set; }
    public string? workOrderCoilPitchControl { get; set; }
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    public string? memo { get; set; }
    public string? originPlace { get; set; }
    public string? phosphatingTemperature { get; set; }
    public string? pieceNo { get; set; }
    public string? productDiameter { get; set; }
    public string? qrCode { get; set; }
    public int? qrTimes { get; set; }
    public string? qualityMaterialId { get; set; }
    public bool resultSaved { get; set; }
    public string? saponificationPhValue { get; set; }
    public string? saponificationTemperature { get; set; }
    public string? shiftCode { get; set; }
    public string? shiftName { get; set; }
    public string? spec { get; set; }
    public string? spoolWeightRequirement { get; set; }
    public string? steelGrade { get; set; }
    public string? strengthMpa { get; set; }
    public string? surfaceCondition { get; set; }
    public string? targetSpecification { get; set; }
    public string? totalAcid { get; set; }
    public string? totalAcidSampling { get; set; }
    public string? upperToleranceValue { get; set; }
    public string? workOrderStatus { get; set; }
    public string? workOrderNo { get; set; }
}
