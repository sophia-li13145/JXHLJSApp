namespace JXHLJSApp.Models.WorkOrders;

public sealed class WorkOrderCompletionStatusDto
{
    public decimal? actualWeight { get; set; }
    public string? completedText { get; set; }
    public bool? isCompleted { get; set; }
    public decimal? plannedWeight { get; set; }

    public string systemStatusDisplay => string.IsNullOrWhiteSpace(completedText)
        ? (isCompleted == true ? "已完工" : "未完工")
        : completedText!.Trim();

    public string targetStatusDisplay => isCompleted == true ? "目标已达成" : "目标未达成";

    public string weightProgressDisplay
    {
        get
        {
            var actual = FormatWeight(actualWeight);
            var planned = FormatWeight(plannedWeight);
            return actual == "--" && planned == "--" ? "--" : $"{actual} / {planned}";
        }
    }

    private static string FormatWeight(decimal? weight) => weight.HasValue ? $"{weight.Value:0.##}" : "--";
}
