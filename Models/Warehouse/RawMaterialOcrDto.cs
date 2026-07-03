namespace JXHLJSApp.Models.Warehouse;

public sealed class RawMaterialOcrDto
{
    public string? qrCode { get; set; }
    public string? coilCount { get; set; }
    public string? coilDiameter { get; set; }
    public string? furnaceNo { get; set; }
    public string? materialName { get; set; }
    public string? materialType { get; set; }
    public string? ocrRawText { get; set; }
    public string? originPlace { get; set; }
    public string? pieceWeight { get; set; }
    public string? pieceWeightUnit { get; set; }
    public string? spec { get; set; }
    public string? strength { get; set; }

    public string materialTitle => $"物料编号： {FirstNonEmpty(materialType, materialName, "--")}";
    public string materialNameDisplay => FirstNonEmpty(materialName, "--");
    public string specDisplay => FirstNonEmpty(spec, "--");
    public string furnaceNoDisplay => FirstNonEmpty(furnaceNo, "--");
    public string originPlaceDisplay => FirstNonEmpty(originPlace, "--");
    public string strengthDisplay => FirstNonEmpty(strength, "--");
    public string coilCountDisplay => FirstNonEmpty(coilCount, "--");
    public string pieceWeightDisplay => JoinNonEmpty(pieceWeight, pieceWeightUnit);
    public string materialTypeDisplay => FirstNonEmpty(materialType, "--");

    private static string JoinNonEmpty(params string?[] values)
    {
        var text = string.Join(" ", values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));
        return string.IsNullOrWhiteSpace(text) ? "--" : text;
    }

    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "--";
}
