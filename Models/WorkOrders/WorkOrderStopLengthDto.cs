using System.Globalization;

namespace JXHLJSApp.Models.WorkOrders;

/// <summary>PDA 生产工单停工产出长度记录。</summary>
public sealed class WorkOrderStopLengthDto
{
    public string? id { get; set; }
    public string? platPlanNo { get; set; }
    public string? workOrderId { get; set; }
    public string? workOrderNo { get; set; }
    public string? productionTaskId { get; set; }
    public string? processCode { get; set; }
    public string? processName { get; set; }
    public decimal? outputLength { get; set; }
    public string? unit { get; set; }
    public string? stopTime { get; set; }
    public string? operatorId { get; set; }
    public string? operatorName { get; set; }
    public string? memo { get; set; }
    public string? createdTime { get; set; }

    public string outputLengthDisplay => outputLength.HasValue
        ? $"{outputLength.Value:0.##}{unit}".Trim()
        : "--";

    public string stopTimeDisplay => FormatTime(stopTime, createdTime);

    public string operatorDisplay => string.IsNullOrWhiteSpace(operatorName) ? "--" : operatorName.Trim();

    private static string FormatTime(params string?[] values)
    {
        var raw = values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "--";
        }

        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : raw;
    }
}
