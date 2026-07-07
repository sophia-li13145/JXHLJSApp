namespace JXHLJSApp.Models.Warehouse;

public sealed class RawMaterialReceivingDetailDto
{
    public string? arrivalNo { get; set; }
    public string? consignor { get; set; }
    public List<RawMaterialReceivingDetailItemDto>? detailList { get; set; }
    public string? factoryCode { get; set; }
    public string? factoryName { get; set; }
    public string? id { get; set; }
    public string? inspectStatus { get; set; }
    public string? instockDate { get; set; }
    public string? instockNo { get; set; }
    public string? instockStatus { get; set; }
    public string? instockStatusName { get; set; }
    public string? memo { get; set; }
    public List<RawMaterialReceivingOcrDto>? ocrList { get; set; }
    public string? @operator { get; set; }
    public string? orderType { get; set; }
    public string? orderTypeName { get; set; }
    public string? purchaseNo { get; set; }
    public string? returnMaterialNo { get; set; }
    public string? supplierCode { get; set; }
    public string? supplierName { get; set; }
    public string? workOrderNo { get; set; }

    public string instockNoDisplay => FirstNonEmpty(instockNo, arrivalNo, "--");
    public string statusDisplay => FirstNonEmpty(instockStatusName, instockStatus, "--");
    public string instockDateDisplay => FirstNonEmpty(instockDate, "--");
    public string warehouseDisplay => FirstNonEmpty(detailList?.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.instockWarehouse))?.instockWarehouse, "--");
    public IReadOnlyList<RawMaterialReceivingDetailItemDto> detailItems => detailList ?? new List<RawMaterialReceivingDetailItemDto>();
    public IReadOnlyList<AttachmentDto> mainAttachments => ocrList?
        .SelectMany(ocr => ocr.attachmentList ?? new List<AttachmentDto>())
        .Where(file => string.Equals(file.attachmentLocation, "main", StringComparison.OrdinalIgnoreCase))
        .ToList() ?? new List<AttachmentDto>();

    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "--";
}

public sealed class RawMaterialReceivingDetailItemDto
{
    public string? coilNo { get; set; }
    public decimal? count { get; set; }
    public string? furnaceNo { get; set; }
    public string? id { get; set; }
    public string? instockNo { get; set; }
    public decimal? instockQty { get; set; }
    public string? instockWarehouse { get; set; }
    public string? instockWarehouseCode { get; set; }
    public string? location { get; set; }
    public string? manufactureName { get; set; }
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    public string? memo { get; set; }
    public string? model { get; set; }
    public string? origin { get; set; }
    public decimal? pieceWeight { get; set; }
    public string? productionBatch { get; set; }
    public string? productionDate { get; set; }
    public string? spec { get; set; }
    public string? stockBatch { get; set; }
    public string? unit { get; set; }
    public string? weight { get; set; }

    public string materialTitle => $"物料编号： {FirstNonEmpty(materialCode, materialName, "--")}";
    public string materialNameDisplay => FirstNonEmpty(materialName, "--");
    public string specDisplay => FirstNonEmpty(spec, model, "--");
    public string furnaceNoDisplay => FirstNonEmpty(furnaceNo, "--");
    public string originDisplay => FirstNonEmpty(origin, "--");
    public string strengthDisplay => FirstNonEmpty(pieceWeight?.ToString("0.##"), "--");
    public string coilCountDisplay => FirstNonEmpty(count?.ToString("0.##"), "--");
    public string pieceWeightDisplay => JoinNonEmpty(FirstNonEmpty(instockQty?.ToString("0.##"), weight), unit);
    public string materialTypeDisplay => FirstNonEmpty(manufactureName, "原料");

    private static string JoinNonEmpty(params string?[] values)
    {
        var text = string.Join(" ", values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));
        return string.IsNullOrWhiteSpace(text) ? "--" : text;
    }

    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "--";
}

public sealed class RawMaterialReceivingOcrDto
{
    public List<AttachmentDto>? attachmentList { get; set; }
    public decimal? coilCount { get; set; }
    public decimal? coilDiameter { get; set; }
    public string? createdTime { get; set; }
    public string? furnaceNo { get; set; }
    public string? id { get; set; }
    public string? instockNo { get; set; }
    public string? materialName { get; set; }
    public string? materialType { get; set; }
    public string? memo { get; set; }
    public string? ocrFailReason { get; set; }
    public string? ocrRawText { get; set; }
    public string? ocrStatus { get; set; }
    public string? originPlace { get; set; }
    public decimal? pieceWeight { get; set; }
    public string? pieceWeightUnit { get; set; }
    public string? spec { get; set; }
    public string? strength { get; set; }
}
