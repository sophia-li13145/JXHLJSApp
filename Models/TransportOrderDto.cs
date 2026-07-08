namespace JXHLJSApp.Models;

public sealed class TransportOrderDto
{
    public string? currentProcess { get; set; }
    public string? materialCode { get; set; }
    public string? materialName { get; set; }
    public string? nextProcess { get; set; }
    public string? productionAddress { get; set; }
    public decimal? quantity { get; set; }
    public string? rawOrQuench { get; set; }
    public string? spec { get; set; }
    public string? steelGrade { get; set; }
    public decimal? totalQuantity { get; set; }
    public decimal? totalWeight { get; set; }
    public string? transportOrderNo { get; set; }
    public string? unit { get; set; }
    public decimal? weight { get; set; }
    public string? workOrderNo { get; set; }
}
