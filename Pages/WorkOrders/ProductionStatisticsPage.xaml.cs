using JXHLJSApp.Models.WorkOrders;
using JXHLJSApp.Services.WorkOrders;
using Microsoft.Maui.Controls.Shapes;
using System.Globalization;

namespace JXHLJSApp.Pages.WorkOrders;

public partial class ProductionStatisticsPage : ContentPage
{
    private readonly IWorkOrderApi _workOrderApi;
    private ProductionStatisticsDto? _statistics;
    private string _month = DateTime.Today.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    public ProductionStatisticsPage(IWorkOrderApi workOrderApi)
    {
        _workOrderApi = workOrderApi;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_statistics is null)
        {
            await LoadStatisticsAsync();
        }
    }

    private async void OnRefreshing(object sender, EventArgs e) => await LoadStatisticsAsync();

    private async Task LoadStatisticsAsync()
    {
        try
        {
            RefreshContainer.IsRefreshing = true;
            _statistics = await _workOrderApi.GetProductionStatisticsAsync(_month);
            _month = string.IsNullOrWhiteSpace(_statistics?.month) ? _month : _statistics!.month!;
            BuildContent();
        }
        catch (Exception ex)
        {
            await DisplayAlert("加载失败", ex.Message, "确定");
        }
        finally
        {
            RefreshContainer.IsRefreshing = false;
        }
    }

    private void BuildContent()
    {
        ContentStack.Children.Clear();
        AddMonthTabs();
        AddSummaryCard();
        ContentStack.Children.Add(new Label { Text = "产出明细列表（按机台/日期汇总）", TextColor = Color.FromArgb("#051B3D"), FontSize = 14, FontAttributes = FontAttributes.Bold });
        AddDetailCards();
    }

    private void AddMonthTabs()
    {
        var months = BuildMonthOptions();
        var row = new HorizontalStackLayout { Spacing = 12 };
        foreach (var month in months)
        {
            var selected = string.Equals(month.Value, _month, StringComparison.Ordinal);
            var button = new Button
            {
                Text = month.Label,
                WidthRequest = 56,
                HeightRequest = 36,
                CornerRadius = 18,
                Padding = 0,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                BackgroundColor = selected ? Color.FromArgb("#214B86") : Colors.White,
                TextColor = selected ? Colors.White : Color.FromArgb("#4B6688"),
                BorderColor = Color.FromArgb("#D6E0EC"),
                BorderWidth = selected ? 0 : 1
            };
            button.Clicked += async (_, _) =>
            {
                _month = month.Value;
                await LoadStatisticsAsync();
            };
            row.Children.Add(button);
        }
        ContentStack.Children.Add(row);
    }

    private IReadOnlyList<(string Label, string Value)> BuildMonthOptions()
    {
        var current = DateTime.Today;
        return Enumerable.Range(0, 2)
            .Select(offset => current.AddMonths(-offset))
            .Select(date => ($"{date.Month}月", date.ToString("yyyy-MM", CultureInfo.InvariantCulture)))
            .Reverse()
            .ToList();
    }

    private void AddSummaryCard()
    {
        var output = _statistics?.workOrderOutput;
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(), new ColumnDefinition(), new ColumnDefinition()),
            ColumnSpacing = 8
        };
        grid.Add(CreateSummaryBox(output?.normalProduction, "正常生产", "#EFFFF5", "#00A651"), 0);
        grid.Add(CreateSummaryBox(output?.redCardRecord, "红牌记录", "#FFF2F2", "#E23636"), 1);
        grid.Add(CreateSummaryBox(output?.smallPieceRecord, "小件记录", "#FFF8E8", "#F59A00"), 2);

        ContentStack.Children.Add(new Border
        {
            BackgroundColor = Colors.White,
            StrokeThickness = 0,
            Padding = new Thickness(16),
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new Label { Text = "汇总概览", TextColor = Color.FromArgb("#051B3D"), FontSize = 14, FontAttributes = FontAttributes.Bold },
                    grid
                }
            }
        });
    }

    private static View CreateSummaryBox(WorkOrderOutputSummaryDto? item, string fallbackName, string bg, string fg)
    {
        return new Border
        {
            BackgroundColor = Color.FromArgb(bg),
            Stroke = Color.FromArgb(fg),
            StrokeThickness = 1,
            Padding = new Thickness(6, 8),
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            Content = new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    new Label { Text = FirstNonEmpty(item?.statisticsName, fallbackName), TextColor = Color.FromArgb(fg), FontSize = 12, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center },
                    new Label { Text = $"{FormatNumber(item?.outputWeight)} 吨", TextColor = Color.FromArgb(fg), FontSize = 15, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center },
                    new Label { Text = $"{FormatNumber(item?.outputCount)} 吊", TextColor = Color.FromArgb("#051B3D"), FontSize = 11, HorizontalTextAlignment = TextAlignment.Center }
                }
            }
        };
    }

    private void AddDetailCards()
    {
        var details = _statistics?.workOrderOutput?.detailList ?? new List<WorkOrderOutputDetailDto>();
        if (details.Count == 0)
        {
            ContentStack.Children.Add(new Label { Text = "暂无产出明细", TextColor = Color.FromArgb("#6F8197"), HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 24) });
            return;
        }

        foreach (var item in details)
        {
            ContentStack.Children.Add(CreateDetailCard(item));
        }
    }

    private static View CreateDetailCard(WorkOrderOutputDetailDto item)
    {
        var statusColor = GetStatusColor(item.statisticsType, item.productInspectStatus, item.statisticsName);
        var title = FirstNonEmpty(item.title, $"{FirstNonEmpty(item.machineNo, "--")} 生产汇总");
        return new Border
        {
            BackgroundColor = Colors.White,
            Stroke = Color.FromArgb("#D8E3F3"),
            StrokeThickness = 1,
            Padding = new Thickness(12),
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto }),
                        Children =
                        {
                            new Label { Text = title, TextColor = Color.FromArgb("#051B3D"), FontSize = 14, FontAttributes = FontAttributes.Bold },
                            CreateStatusBadge(FirstNonEmpty(item.statisticsName, item.productInspectStatus, "正常"), statusColor)
                        }
                    },
                    CreateInfoGrid(item)
                }
            }
        };
    }

    private static View CreateStatusBadge(string text, Color color)
    {
        var badge = new Border
        {
            BackgroundColor = Color.FromRgba(color.Red, color.Green, color.Blue, 0.12f),
            StrokeThickness = 0,
            Padding = new Thickness(8, 3),
            StrokeShape = new RoundRectangle { CornerRadius = 5 },
            Content = new Label { Text = text, TextColor = color, FontSize = 11, FontAttributes = FontAttributes.Bold }
        };
        Grid.SetColumn(badge, 1);
        return badge;
    }

    private static Grid CreateInfoGrid(WorkOrderOutputDetailDto item)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(), new ColumnDefinition()), RowSpacing = 8, ColumnSpacing = 8 };
        AddInfo(grid, 0, 0, "机台", FirstNonEmpty(item.machineNo, "--"));
        AddInfo(grid, 0, 1, "生产日期", FirstNonEmpty(item.productionDate, "--"));
        AddInfo(grid, 1, 0, "钢号", FirstNonEmpty(item.steelGrade, "--"));
        AddInfo(grid, 1, 1, "规格", FirstNonEmpty(item.specification, "--"));
        AddInfo(grid, 2, 0, "当天总重量", $"{FormatNumber(item.outputWeight)} 吨", Color.FromArgb("#00A651"));
        return grid;
    }

    private static void AddInfo(Grid grid, int row, int column, string label, string value, Color? valueColor = null)
    {
        while (grid.RowDefinitions.Count <= row)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
        grid.Add(new HorizontalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label { Text = label, TextColor = Color.FromArgb("#536984"), FontSize = 12 },
                new Label { Text = value, TextColor = valueColor ?? Color.FromArgb("#001431"), FontSize = 12, FontAttributes = FontAttributes.Bold }
            }
        }, column, row);
    }

    private static Color GetStatusColor(string? type, string? inspectStatus, string? name)
    {
        var text = $"{type}{inspectStatus}{name}";
        if (text.Contains("红", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb("#E23636");
        if (text.Contains("小", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb("#F59A00");
        return Color.FromArgb("#00A651");
    }

    private static string FormatNumber(decimal? value) => value.HasValue ? value.Value.ToString("0.##", CultureInfo.InvariantCulture) : "0";
    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
