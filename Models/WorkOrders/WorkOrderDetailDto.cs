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
}

public sealed class WorkOrderMoldSequenceDto
{
    public decimal? moldSequence { get; set; }
    public decimal? pieceWeight { get; set; }
    public decimal? productionQuantity { get; set; }
    public decimal? productionWeight { get; set; }
}
