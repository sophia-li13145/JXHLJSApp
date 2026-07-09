namespace JXHLJSApp.Models.WorkOrders;

public sealed class ProductionStatisticsDto
{
    public List<ProductionStatisticsDateDto>? dateList { get; set; }
    public string? month { get; set; }
    public string? selectedDate { get; set; }
    public WorkOrderOutputStatisticsDto? workOrderOutput { get; set; }
}

public sealed class ProductionStatisticsDateDto
{
    public string? date { get; set; }
    public bool isSelected { get; set; }
    public bool isThisMonth { get; set; }
    public string? monthName { get; set; }
}

public sealed class WorkOrderOutputStatisticsDto
{
    public List<WorkOrderOutputDetailDto>? detailList { get; set; }
    public WorkOrderOutputSummaryDto? normalProduction { get; set; }
    public WorkOrderOutputSummaryDto? redCardRecord { get; set; }
    public WorkOrderOutputSummaryDto? smallPieceRecord { get; set; }
}

public sealed class WorkOrderOutputSummaryDto
{
    public decimal? outputCount { get; set; }
    public decimal? outputWeight { get; set; }
    public string? statisticsName { get; set; }
    public string? statisticsType { get; set; }
}

public sealed class WorkOrderOutputDetailDto
{
    public string? machineNo { get; set; }
    public decimal? outputCount { get; set; }
    public decimal? outputWeight { get; set; }
    public string? productInspectStatus { get; set; }
    public string? productionDate { get; set; }
    public string? specification { get; set; }
    public string? statisticsName { get; set; }
    public string? statisticsType { get; set; }
    public string? steelGrade { get; set; }
    public string? title { get; set; }
}
