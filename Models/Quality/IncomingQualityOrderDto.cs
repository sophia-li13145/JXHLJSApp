namespace JXHLJSApp.Models.Quality;

public sealed class IncomingQualityOrderDto
{
    public int? done { get; set; }
    public string? incomingQualityNo { get; set; }
    public string? instockNo { get; set; }
    public int? total { get; set; }
    public string? delStatus { get; set; }
    public string? delStatusName { get; set; }
    public string? materialName { get; set; }
    public string? materialCode { get; set; }
    public string? specification { get; set; }

    public string incomingQualityNoDisplay => string.IsNullOrWhiteSpace(incomingQualityNo) ? "未生成来料质检单" : incomingQualityNo!;
    public string instockNoDisplay => string.IsNullOrWhiteSpace(instockNo) ? "-" : instockNo!;
    public string statusDisplay => string.IsNullOrWhiteSpace(delStatusName) ? (delStatus ?? "未提交") : delStatusName!;
    public string totalDisplay => total.HasValue ? $"{total}件" : "-";
    public string doneDisplay => done.HasValue ? $"已扫记录：{done} 条" : "已扫记录：-";
    public string materialDisplay
    {
        get
        {
            var parts = new[] { materialName, materialCode, specification }.Where(v => !string.IsNullOrWhiteSpace(v));
            var text = string.Join(" ", parts);
            return string.IsNullOrWhiteSpace(text) ? "-" : text;
        }
    }
    public bool hasMaterial => !string.IsNullOrWhiteSpace(materialDisplay) && materialDisplay != "-";
}

public sealed class IncomingQualityOrderDetailDto
{
    public List<IncomingQualityScanDetailDto>? detailList { get; set; }
    public string? incomingQualityNo { get; set; }
    public string? instockNo { get; set; }
    public string? materialName { get; set; }
    public string? spec { get; set; }
    public int? total { get; set; }
    public string? delStatus { get; set; }
    public string? delStatusName { get; set; }

    public string incomingQualityNoDisplay => string.IsNullOrWhiteSpace(incomingQualityNo) ? "-" : incomingQualityNo!;
    public string instockNoDisplay => string.IsNullOrWhiteSpace(instockNo) ? "-" : instockNo!;
    public string statusDisplay => string.IsNullOrWhiteSpace(delStatusName) ? (delStatus ?? "未提交") : delStatusName!;
    public int? scanCount => detailList?.Count ?? 0;
}

public sealed class IncomingQualityScanDetailDto
{
    public string? otherProblemItem { get; set; }
    public string? problemPoint { get; set; }
    public string? qrCode { get; set; }
    public string? inspectResult { get; set; }
    public string? inspectResultName { get; set; }
    public string problemPointDisplay => string.IsNullOrWhiteSpace(problemPoint) ? "-" : problemPoint!;
    public string qrCodeDisplay => string.IsNullOrWhiteSpace(qrCode) ? "-" : qrCode!;
    public string inspectResultDisplay => string.IsNullOrWhiteSpace(inspectResultName) ? (inspectResult ?? "-") : inspectResultName!;
    public Color inspectResultColor => inspectResultDisplay.Contains("合格") && !inspectResultDisplay.Contains("不合格") ? Color.FromArgb("#00A86B") : Color.FromArgb("#FF4D5E");
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
    public string? spec { get; set; }

    public string qrCodeDisplay => string.IsNullOrWhiteSpace(qrCode) ? "-" : qrCode!;
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
