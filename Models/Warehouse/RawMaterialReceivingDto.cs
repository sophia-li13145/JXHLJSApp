namespace JXHLJSApp.Models.Warehouse;

public sealed class RawMaterialReceivingDto
{
    public string? arrivalNo { get; set; }
    public string? createdTime { get; set; }
    public decimal? detailCount { get; set; }
    public string? id { get; set; }
    public string? instockNo { get; set; }
    public string? instockStatus { get; set; }
    public string? instockStatusName { get; set; }
    public string? instockWarehouses { get; set; }
    public string? materialNames { get; set; }
    public string? orderType { get; set; }
    public string? orderTypeName { get; set; }
    public string? origins { get; set; }
    public string? purchaseNo { get; set; }
    public string? supplierName { get; set; }
    public string? workOrderNo { get; set; }

    public string instockNoDisplay => string.IsNullOrWhiteSpace(instockNo) ? "--" : instockNo;
    public string materialDisplay => string.IsNullOrWhiteSpace(materialNames) ? "--" : materialNames;
    public string detailCountDisplay => detailCount.HasValue ? $"共 {detailCount.Value:0.##} 件" : "共 -- 件";
    public string warehouseDisplay => string.IsNullOrWhiteSpace(instockWarehouses) ? "--" : instockWarehouses;
    public string statusDisplay => FirstNonEmpty(instockStatusName, instockStatus, "--");
    public bool canCancel => string.Equals(instockStatus, "0", StringComparison.OrdinalIgnoreCase);
    public string statusBackgroundColor => string.Equals(instockStatus, "2", StringComparison.OrdinalIgnoreCase) ? "#EAFBF0" : "#F2F4F8";
    public string statusTextColor => string.Equals(instockStatus, "2", StringComparison.OrdinalIgnoreCase) ? "#09913A" : "#20324A";

    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "--";
}
