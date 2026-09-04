using System.Text.Json.Serialization;
using JXHLJSApp.Models.Warehouse;

namespace JXHLJSApp.Models;

public sealed class MaterialScanQueryDto
{
    // 后端会根据物料状态选择性返回以下模块；三个实体彼此独立，均允许为 null。
    public MaterialScanBasicInfoDto? basicInfo { get; set; } = null;
    public MaterialScanInspectionInfoDto? inspectionInfo { get; set; } = null;
    public MaterialScanInstructionCardInfoDto? instructionCardInfo { get; set; } = null;
}

public sealed class MaterialScanBasicInfoDto
{
    public string? furnaceNo { get; set; }
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    public string? materialState { get; set; }
    public string? origin { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? packagePieceWeight { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? pieceWeight { get; set; }
    public string? spec { get; set; }
}

public sealed class MaterialScanInspectionInfoDto
{
    public string? actualDiameterMm { get; set; }
    public string? brokenDiameter { get; set; }
    public string? coilDiameterControl { get; set; }
    public string? coilPitchControl { get; set; }
    public bool? continuouslyUnqualified { get; set; }
    public string? elongationRate { get; set; }
    public bool? employeeIntervention { get; set; }
    public string? inspectDate { get; set; }
    public string? inspectResult { get; set; }
    public string? inspector { get; set; }
    public string? memo { get; set; }
    public string? otherProblemItem { get; set; }
    public string? problemPoint { get; set; }
    public string? reductionOfAreaRate { get; set; }
    public string? strengthMpa { get; set; }
    public string? surfaceCondition { get; set; }
    public string? torsion { get; set; }
    public string? unqualifiedDescription { get; set; }
    public bool? isQualified { get; set; }
}

public sealed class MaterialScanInstructionCardInfoDto
{
    public string? billetLowerTolerance { get; set; }
    public string? billetUpperTolerance { get; set; }
    public string? blankSpecification { get; set; }
    public string? coilDiameterControl { get; set; }
    public string? coilPitchControl { get; set; }
    public string? customerIdentifier { get; set; }
    public string? drawMode { get; set; }
    public string? dvSpeed { get; set; }
    public string? inputSpecification { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? pieceWeight { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? outputLength { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? outputWeight { get; set; }
    public string? ovalityControl { get; set; }
    public string? rawOrQuench { get; set; }
    public string? saleMode { get; set; }
    public string? strengthRange { get; set; }
    public string? torsion { get; set; }
    public string? wireShape { get; set; }
    public string? wireTakeUpLength { get; set; }
    public string? wireTakeUpSpeed { get; set; }
    public string? wireTakeUpSpeedUnit { get; set; }
}
