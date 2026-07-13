namespace JXHLJSApp.Models.Warehouse;

public sealed class RawMaterialOcrDto
{
    public string? qrCode { get; set; }
    public string? coilCount { get; set; }
    public string? coilDiameter { get; set; }
    public string? coilNo { get; set; }
    public string? companyName { get; set; }
    public decimal? confidence { get; set; }
    public string? furnaceNo { get; set; }
    public string? materialClass { get; set; }
    public string? materialClassName { get; set; }
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    public string? materialType { get; set; }
    public string? ocrRawText { get; set; }
    public string? originPlace { get; set; }
    public string? pieceWeight { get; set; }
    public string? pieceWeightUnit { get; set; }
    public string? productName { get; set; }
    public string? productionDate { get; set; }
    public string? spec { get; set; }
    public string? standard { get; set; }
    public string? strength { get; set; }
    public string? workshop { get; set; }

    public string materialClassDisplay => FirstNonEmpty(materialClassName, materialClass, materialType, "--");
    public bool isSemiFinished => ContainsAny(materialClass, "半成品", "semi") || ContainsAny(materialClassName, "半成品", "semi") || ContainsAny(materialType, "半成品", "semi");
    public string materialTitle => $"追溯码： {FirstNonEmpty(qrCode, materialCode, materialName, "--")}";
    public string materialNameDisplay => FirstNonEmpty(materialName, "--");
    public string specDisplay => FirstNonEmpty(spec, "--");
    public string furnaceNoDisplay => FirstNonEmpty(furnaceNo, "--");
    public string originPlaceDisplay => FirstNonEmpty(originPlace, "--");
    public string strengthDisplay => FirstNonEmpty(strength, "--");
    public string coilCountDisplay => FirstNonEmpty(coilCount, "--");
    public string coilDiameterDisplay => FirstNonEmpty(coilDiameter, "--");
    public string pieceWeightDisplay => JoinNonEmpty(pieceWeight, pieceWeightUnit);
    public string materialTypeDisplay => FirstNonEmpty(materialType, "--");

    private static string JoinNonEmpty(params string?[] values)
    {
        var text = string.Join(" ", values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));
        return string.IsNullOrWhiteSpace(text) ? "--" : text;
    }

    private static bool ContainsAny(string? value, params string[] tokens) =>
        !string.IsNullOrWhiteSpace(value) && tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "--";
}
