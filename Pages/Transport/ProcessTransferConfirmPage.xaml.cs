using JXHLJSApp.Models;
using System.Globalization;

namespace JXHLJSApp.Pages.Transport;

public partial class ProcessTransferConfirmPage : ContentPage
{
    private TransportOrderDto? _order;

    public ProcessTransferConfirmPage(Services.Transport.ITransportOrderApi transportOrderApi)
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _order = TransportOrderNavigationStore.Current;
        BindOrder();
    }

    private void BindOrder()
    {
        MaterialNameLabel.Text = $"物料名称：{FirstNonEmpty(_order?.materialName, _order?.materialCode, "--")}";
        SpecLabel.Text = $"规格：{FirstNonEmpty(_order?.spec, "--")}";
        ProductionAddressLabel.Text = $"产地：{FirstNonEmpty(_order?.productionAddress, "--")}";
        QuantityLabel.Text = $"件数：{FormatQuantity(_order?.totalQuantity ?? _order?.quantity, _order?.unit)}";
        WeightLabel.Text = $"重量：{FormatWeight(_order?.totalWeight ?? _order?.weight)}";
        WorkOrderNoLabel.Text = $"工单：{FirstNonEmpty(_order?.workOrderNo, "--")}";
        CurrentProcessLabel.Text = $"{FirstNonEmpty(_order?.currentProcess, "--")}  |  机台：{FirstNonEmpty(_order?.currentMachineName, _order?.currentMachineNo, "--")}";
        NextProcessLabel.Text = $"{FirstNonEmpty(_order?.nextProcess, "--")}  |  机台：{FirstNonEmpty(_order?.nextMachineName, _order?.nextMachineNo, "--")}";
        BindTraceList();
    }

    private void BindTraceList()
    {
        TraceListLayout.Clear();
        var traces = (_order?.operationTraceList ?? new List<TransportOperationTraceDto>())
            .OrderBy(t => t.operationSeq ?? decimal.MaxValue)
            .ThenBy(t => t.startTime)
            .ToList();

        if (traces.Count == 0)
        {
            TraceListLayout.Add(new Label { Text = "暂无执行追踪记录", TextColor = Color.FromArgb("#667A96"), FontSize = 13 });
            return;
        }

        for (var i = 0; i < traces.Count; i++)
        {
            TraceListLayout.Add(CreateTraceItem(traces[i], i + 1, i < traces.Count - 1));
        }
    }

    private static View CreateTraceItem(TransportOperationTraceDto trace, int index, bool showLine)
    {
        var palette = ResolveTracePalette(trace.executionStatus);
        var root = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(24), new(GridLength.Star) }, RowDefinitions = new RowDefinitionCollection { new(GridLength.Auto), new(GridLength.Auto) } };
        root.Add(new Border { WidthRequest = 13, HeightRequest = 13, Stroke = palette.Color, StrokeThickness = 1.5, BackgroundColor = Colors.White, StrokeShape = new RoundRectangle { CornerRadius = 8 }, Content = new BoxView { WidthRequest = 5, HeightRequest = 5, Color = palette.Color, CornerRadius = 3, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Start, Margin = new Thickness(0, 3, 0, 0) }, 0, 0);
        if (showLine) root.Add(new BoxView { WidthRequest = 2, Color = Color.FromArgb("#DDE5EF"), HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 18, 0, 0) }, 0, 1);

        var title = new Label { Text = $"{index}. {FirstNonEmpty(trace.operationName, "--")}（{palette.Text}）", TextColor = palette.Color, FontSize = 14, FontAttributes = FontAttributes.Bold };
        root.Add(title, 1, 0);

        var card = new Border { BackgroundColor = Color.FromArgb("#F6F8FB"), StrokeThickness = 0, Padding = new Thickness(12, 10), Margin = new Thickness(0, 8, 0, 14), StrokeShape = new RoundRectangle { CornerRadius = 7 } };
        var rows = new VerticalStackLayout { Spacing = 6 };
        AddInfoRow(rows, "执行班组：", FirstNonEmpty(trace.shiftName, trace.shiftCode, "--"));
        AddInfoRow(rows, "执行机台：", FirstNonEmpty(trace.machineName, trace.machineNo, trace.machineCode, "--"));
        AddInfoRow(rows, "开始时间：", FormatDateTime(trace.startTime), hideWhenEmpty: true);
        AddInfoRow(rows, "结束时间：", FormatDateTime(trace.finishTime), hideWhenEmpty: true);
        card.Content = rows;
        root.Add(card, 1, 1);
        return root;
    }

    private static void AddInfoRow(VerticalStackLayout rows, string label, string value, bool hideWhenEmpty = false)
    {
        if (hideWhenEmpty && string.IsNullOrWhiteSpace(value)) return;
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(86), new(GridLength.Star) } };
        grid.Add(new Label { Text = label, TextColor = Color.FromArgb("#667A96"), FontSize = 12 }, 0, 0);
        grid.Add(new Label { Text = string.IsNullOrWhiteSpace(value) ? "--" : value, TextColor = Color.FromArgb("#001431"), FontSize = 12, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.End }, 1, 0);
        rows.Add(grid);
    }

    private async void OnCancelTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private static (Color Color, string Text) ResolveTracePalette(string? status)
    {
        var normalized = status?.Trim();
        if (normalized is "已完成" or "完成" or "completed" or "complete" or "2") return (Color.FromArgb("#00BD74"), "已完成");
        if (normalized is "执行中" or "进行中" or "running" or "1") return (Color.FromArgb("#FFAA1D"), "执行中");
        if (normalized is "异常" or "abnormal" or "4") return (Color.FromArgb("#E5484D"), "异常");
        if (normalized is "已暂停" or "暂停" or "paused" or "3") return (Color.FromArgb("#FFAA1D"), "已暂停");
        if (normalized is "已取消" or "取消" or "cancelled" or "5") return (Color.FromArgb("#9AA8BA"), "已取消");
        return (Color.FromArgb("#667A96"), "未开始");
    }

    private static string FormatQuantity(decimal? value, string? unit) => value.HasValue ? $"{value.Value.ToString("N0", CultureInfo.InvariantCulture)}{FirstNonEmpty(unit, "件")}" : "--";
    private static string FormatWeight(decimal? value) => value.HasValue ? $"{value.Value.ToString("N0", CultureInfo.InvariantCulture)}kg" : "--";
    private static string FormatDateTime(string? value) => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt) ? dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : FirstNonEmpty(value, string.Empty);
    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
