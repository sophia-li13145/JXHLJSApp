using JXHLJSApp.Models.Warehouse;
using System.Text.Json.Serialization;

namespace JXHLJSApp.Models;

public sealed class MaterialOutstockTransportOrderDto
{
    public string? fromWarehouseName { get; set; }
    public string? id { get; set; }
    public string? materialName { get; set; }
    public string? materialRequisitionNo { get; set; }
    public string? productionAddress { get; set; }
    public string? sourceBizNo { get; set; }
    public string? spec { get; set; }
    public string? steelGrade { get; set; }
    public string? taskStatus { get; set; }
    public string? toWarehouseName { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? totalQuantity { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? totalWeight { get; set; }
    public string? transportOrderNo { get; set; }

    public string orderNoDisplay => string.IsNullOrWhiteSpace(transportOrderNo) ? "--" : transportOrderNo!;
    public string statusDisplay => string.IsNullOrWhiteSpace(taskStatus) ? "出库完成" : taskStatus!;
    public string summaryDisplay
    {
        get
        {
            var parts = new[]
            {
                string.IsNullOrWhiteSpace(materialRequisitionNo) ? null : $"领料单 {materialRequisitionNo}",
                BuildRouteText(),
                BuildMaterialText()
            }.Where(part => !string.IsNullOrWhiteSpace(part));
            var text = string.Join(" | ", parts);
            return string.IsNullOrWhiteSpace(text) ? "--" : text;
        }
    }

    public Color statusBackgroundColor => Color.FromArgb("#EAFBEF");
    public Color statusTextColor => Color.FromArgb("#00A86B");

    private string? BuildRouteText()
    {
        if (string.IsNullOrWhiteSpace(fromWarehouseName) && string.IsNullOrWhiteSpace(toWarehouseName))
        {
            return null;
        }

        return $"{Display(fromWarehouseName)} → {Display(toWarehouseName)}";
    }

    private string? BuildMaterialText()
    {
        var material = string.Join(" ", new[] { steelGrade, spec, materialName }.Where(v => !string.IsNullOrWhiteSpace(v)));
        return string.IsNullOrWhiteSpace(material) ? null : material;
    }

    private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "--" : value!;
}

public sealed class MaterialOutstockTransportOrderDetailDto
{
    public List<MaterialOutstockTransportOrderDetailItemDto>? detailList { get; set; }
    public string? fromLocationName { get; set; }
    public string? fromWarehouseName { get; set; }
    public string? id { get; set; }
    public string? instockNo { get; set; }
    public string? materialRequisitionNo { get; set; }
    public string? routeCode { get; set; }
    public string? routeName { get; set; }
    public string? taskStatus { get; set; }
    public string? toLocationName { get; set; }
    public string? toWarehouseName { get; set; }
    public string? transportOrderNo { get; set; }

    public string orderNoDisplay => Display(transportOrderNo);
    public string statusDisplay => string.IsNullOrWhiteSpace(taskStatus) ? "出库完成" : taskStatus!;
    public string routeDisplay => string.IsNullOrWhiteSpace(routeName) ? Display(routeCode) : routeName!;
    public string fromDisplay => JoinLocation(fromWarehouseName, fromLocationName);
    public string toDisplay => JoinLocation(toWarehouseName, toLocationName);

    private static string JoinLocation(string? warehouse, string? location)
    {
        var text = string.Join(" / ", new[] { warehouse, location }.Where(v => !string.IsNullOrWhiteSpace(v)));
        return string.IsNullOrWhiteSpace(text) ? "--" : text;
    }

    private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "--" : value!;
}

public sealed class MaterialOutstockTransportOrderDetailItemDto
{
    public string? id { get; set; }
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? quantity { get; set; }
    public string? spec { get; set; }
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? weight { get; set; }

    public string materialCodeDisplay => Display(materialCode);
    public string materialNameDisplay => Display(materialName);
    public string specDisplay => Display(spec);
    public string quantityDisplay => quantity.HasValue ? quantity.Value.ToString("0.##") : "--";
    public string weightDisplay => weight.HasValue ? weight.Value.ToString("0.##") : "--";

    private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "--" : value!;
}
