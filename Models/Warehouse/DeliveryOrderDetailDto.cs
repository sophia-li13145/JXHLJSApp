using System.Text.Json.Serialization;

namespace JXHLJSApp.Models.Warehouse;

public sealed class DeliveryOrderDetailDto
{
    public List<DeliveryOrderScanDetailDto>? actualScanDetailList { get; set; }
    public string? auditStatus { get; set; }
    public string? auditStatusName { get; set; }
    public string? carrierLicense { get; set; }
    public string? carrierName { get; set; }
    public string? consAddress { get; set; }
    public string? customer { get; set; }
    public string? customerNo { get; set; }
    public string? deliveryNo { get; set; }
    public List<DeliveryOrderMaterialDetailDto>? detailList { get; set; }
    public string? expectedDeliveryTime { get; set; }
    public string? id { get; set; }
    public string? logisticsNumber { get; set; }
    public string? logisticsContacter { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? scannedQty { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? totalCount { get; set; }

    public string deliveryNoDisplay => ValueOrDash(deliveryNo);
    public string customerDisplay => ValueOrDash(customer);
    public string consAddressDisplay => ValueOrDash(consAddress);
    public string carrierNameDisplay => ValueOrDash(carrierName);
    public string carrierLicenseDisplay => ValueOrDash(carrierLicense);
    public string logisticsNumberDisplay => ValueOrDash(logisticsNumber);
    public string logisticsContacterDisplay => ValueOrDash(logisticsContacter);
    public string expectedDeliveryDateDisplay => FormatDate(expectedDeliveryTime);
    public string auditStatusDisplay
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(auditStatusName))
            {
                return auditStatusName!;
            }

            return string.IsNullOrWhiteSpace(auditStatus) ? "待发货复核" : auditStatus!;
        }
    }
    public decimal? needScanCount => totalCount;
    public decimal? scannedCount => scannedQty;
    public string scanProgressDisplay => $"{FormatQuantity(scannedCount)} / {FormatQuantity(needScanCount)} 件";

    private static string ValueOrDash(string? value) => string.IsNullOrWhiteSpace(value) ? "--" : value!;

    private static string FormatQuantity(decimal? value) =>
        value.HasValue ? value.Value.ToString("0.##") : "--";

    private static string FormatDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "--";
        return DateTime.TryParse(value, out var date) ? date.ToString("yyyy-MM-dd") : value;
    }
}

public sealed class DeliveryOrderMaterialDetailDto
{
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? curDeliveryQty { get; set; }
    public string? deliveryNo { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? deliveryQty { get; set; }
    public string? id { get; set; }
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    public string? model { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? needDeliveryQty { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? scannedQty { get; set; }
    public string? spec { get; set; }
    public string? stockBatch { get; set; }
    public string? unit { get; set; }

    public bool isScanned => (scannedQty ?? 0) > 0;
    public string scanStatusDisplay => isScanned ? "✓ 已复核" : "待复核";
    public Color scanStatusColor => isScanned ? Color.FromArgb("#00A86B") : Color.FromArgb("#F06423");
    public string materialCodeDisplay => ValueOrDash(materialCode);
    public string materialNameDisplay => ValueOrDash(materialName);
    public string modelDisplay => ValueOrDash(model);
    public string needDeliveryQtyDisplay => FormatQuantity(needDeliveryQty, unit);
    public string scannedQtyDisplay => FormatQuantity(scannedQty, unit);
    public string specDisplay => ValueOrDash(spec);
    public string stockBatchDisplay => ValueOrDash(stockBatch);

    private static string ValueOrDash(string? value) => string.IsNullOrWhiteSpace(value) ? "--" : value!;

    private static string FormatQuantity(decimal? value, string? unit)
    {
        if (!value.HasValue) return "--";
        var text = value.Value % 1 == 0 ? value.Value.ToString("0") : value.Value.ToString("0.##");
        return string.IsNullOrWhiteSpace(unit) ? text : $"{text} {unit}";
    }
}

public sealed class DeliveryOrderScanDetailDto
{
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? actualQty { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? actualWeight { get; set; }
    public string? barcode { get; set; }
    public string? barcodeType { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? count { get; set; }
    public string? deliveryDetailId { get; set; }
    public string? deliveryNo { get; set; }
    public string? id { get; set; }
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    public string? memo { get; set; }
    public string? model { get; set; }
    public string? outstockNo { get; set; }
    public string? orderTime { get; set; }
    public string? scanTime { get; set; }
    public string? scanUserId { get; set; }
    public string? scanUserName { get; set; }
    public string? spec { get; set; }
    public string? stockBatch { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? totalAmount { get; set; }
    public string? unit { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? unitPrice { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? weight { get; set; }

    public string materialCodeDisplay => ValueOrDash(materialCode);
    public string materialNameDisplay => ValueOrDash(materialName);
    public string specDisplay => ValueOrDash(spec);
    public string countDisplay => FormatNumber(count);
    public string weightDisplay => FormatNumberWithUnit(actualWeight, unit);
    public string unitPriceDisplay => FormatMoney(unitPrice);
    public string totalAmountDisplay => FormatMoney(totalAmount);
    public string orderDateDisplay => FormatDate(orderTime);

    private static string ValueOrDash(string? value) => string.IsNullOrWhiteSpace(value) ? "--" : value!;

    private static string FormatNumber(decimal? value) =>
        value.HasValue ? value.Value.ToString("0.##") : "--";

    private static string FormatNumberWithUnit(decimal? value, string? unit)
    {
        if (!value.HasValue) return "--";
        return string.IsNullOrWhiteSpace(unit)
            ? FormatNumber(value)
            : $"{FormatNumber(value)} {unit}";
    }

    private static string FormatMoney(decimal? value) =>
        value.HasValue ? $"¥{value.Value:0.##}" : "--";

    private static string FormatDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "--";
        return DateTime.TryParse(value, out var date) ? date.ToString("yyyy-MM-dd") : value;
    }
}


public sealed class DeliveryOrderScanActualRequestDto
{
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? actualQty { get; set; }
    public string? qrCode { get; set; }
    public string? deliveryNo { get; set; }
    public string? memo { get; set; }
    public string? outstockNo { get; set; }
}

public sealed class DeliveryOrderScanActualResultDto
{
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? actualQty { get; set; }
    public string? barcode { get; set; }
    public string? deliveryNo { get; set; }
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? needDeliveryQty { get; set; }
    public string? scanDetailId { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? scannedQty { get; set; }
    public string? stockBatch { get; set; }
}
