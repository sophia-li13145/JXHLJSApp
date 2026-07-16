using JXHLJSApp.Models.Warehouse;

namespace JXHLJSApp.Models.WorkOrders;

public sealed class WorkOrderAbnormalOptionDto
{
    public string? value { get; set; }
    public string? name { get; set; }
}

public sealed class WorkOrderAbnormalAddRequestDto
{
    public List<AttachmentDto>? abnormalRecordAttachmentList { get; set; }
    public string? abnormalType { get; set; }
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    public string? materialType { get; set; }
    public string? qrCode { get; set; }
    public string? reportMode { get; set; }
    public string? reworkReason { get; set; }
    public string? supplementaryDescription { get; set; }
    public decimal? weight { get; set; }
    public string? workOrderNo { get; set; }
}
