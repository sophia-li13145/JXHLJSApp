using System.Text.Json.Serialization;

namespace JXHLJSApp.Models.Warehouse;

public sealed class DeliveryOrderDto
{
    public string? id { get; set; }
    public string? auditStatus { get; set; }
    public string? customer { get; set; }
    public string? deliveryNo { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? deliveryQty { get; set; }

    public string deliveryNoDisplay => string.IsNullOrWhiteSpace(deliveryNo) ? "--" : deliveryNo!;
    public string customerDisplay => string.IsNullOrWhiteSpace(customer) ? "--" : customer!;
    public string deliveryQtyDisplay => deliveryQty.HasValue ? $"{deliveryQty.Value} 件" : "--";
    public string auditStatusDisplay => string.IsNullOrWhiteSpace(auditStatus) ? "待发货" : auditStatus!;
    public Color statusBackgroundColor => Color.FromArgb("#FFF0DF");
    public Color statusTextColor => Color.FromArgb("#F06423");
}
