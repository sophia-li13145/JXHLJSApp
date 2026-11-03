namespace JXHLJSApp.Models.Quality;

public sealed class QualityCodeRecordPageDto
{
    public long? pageNo { get; set; }
    public long? pageSize { get; set; }
    public long? total { get; set; }
    public List<QualityCodeRecordDto>? records { get; set; }
}

public sealed class QualityCodeRecordDto
{
    public bool? canCancel { get; set; }
    public string? cancelTime { get; set; }
    public string? creator { get; set; }
    public string? id { get; set; }
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    public string? recordNo { get; set; }
    public string? sourceQrCode { get; set; }
    public string? sourceQrStatus { get; set; }
    public string? sourceQrStatusName { get; set; }
    public int? sourceQrTimes { get; set; }
    public string? spec { get; set; }
    public string? status { get; set; }
    public string? statusName { get; set; }
    public string? targetQrCode { get; set; }
    public int? targetQrTimes { get; set; }
    public string? transTime { get; set; }
}
