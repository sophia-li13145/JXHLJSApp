namespace JXHLJSApp.Models;

public sealed class IncomingBarcodeParseResponse
{
    public int code { get; set; }
    public long costTime { get; set; }
    public string? message { get; set; }
    public IncomingBarcodeParseResult? result { get; set; }
    public bool success { get; set; }
}

public sealed class IncomingBarcodeParseResult
{
    public string? barcode { get; set; }
    public string? coilNo { get; set; }
    public string? furnaceNo { get; set; }
    public string? materialCode { get; set; }
    public string? materialId { get; set; }
    public string? materialName { get; set; }
    public string? origin { get; set; }
    public string? productionDate { get; set; }
    public decimal? qty { get; set; }
    public string? spec { get; set; }
    public bool? success { get; set; }
}

public sealed class IncomingSubmitPendingResponse
{
    public int code { get; set; }
    public long costTime { get; set; }
    public string? message { get; set; }
    public bool? result { get; set; }
    public bool? success { get; set; }
}

public sealed class IncomingPendingStockRequest
{
    public string? coilNo { get; set; }
    public string? furnaceNo { get; set; }
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    public string? origin { get; set; }
    public string? productionDate { get; set; }
    public decimal? qty { get; set; }
    public string? spec { get; set; }
}
