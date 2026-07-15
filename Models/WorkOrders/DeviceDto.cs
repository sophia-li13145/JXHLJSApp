namespace JXHLJSApp.Models.WorkOrders;

public sealed class DeviceDto
{
    public string? defaultProcessCode { get; set; }
    public string? defaultProcessName { get; set; }
    public string? devAdministrator { get; set; }
    public string? devCode { get; set; }
    public string? devModel { get; set; }
    public string? devName { get; set; }
    public string? devProducer { get; set; }
    public string? devTypeCode { get; set; }
    public string? devTypeId { get; set; }
    public string? devTypeName { get; set; }
    public string? line { get; set; }
    public string? lineId { get; set; }
    public string? lineName { get; set; }
    public string? machineNo { get; set; }
    public string? manageDeptId { get; set; }
    public string? manageDeptName { get; set; }
    public string? workShopId { get; set; }
    public string? workShopName { get; set; }

    public string displayName => FirstNonEmpty(devName, machineNo, devCode, "未命名机台");

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
