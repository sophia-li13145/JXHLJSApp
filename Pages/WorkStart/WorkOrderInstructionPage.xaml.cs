using JXHLJSApp.Models.WorkOrders;
using JXHLJSApp.Services.WorkOrders;
using Microsoft.Maui.Controls.Shapes;

namespace JXHLJSApp.Pages.WorkStart;

[QueryProperty(nameof(WorkOrderId), "id")]
public partial class WorkOrderInstructionPage : ContentPage
{
    private readonly IWorkOrderApi _workOrderApi;
    private string? _workOrderId;

    public string? WorkOrderId
    {
        get => _workOrderId;
        set
        {
            _workOrderId = Uri.UnescapeDataString(value ?? string.Empty);
            _ = LoadDetailAsync();
        }
    }

    public WorkOrderInstructionPage(IWorkOrderApi workOrderApi)
    {
        InitializeComponent();
        _workOrderApi = workOrderApi;
    }

    private async Task LoadDetailAsync()
    {
        if (string.IsNullOrWhiteSpace(_workOrderId))
        {
            await DisplayAlert("提示", "工单列表主键为空，无法查看生产作业指令卡。", "确定");
            return;
        }

        try
        {
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;
            var detail = await _workOrderApi.GetWorkOrderDetailAsync(_workOrderId);
            if (detail is null)
            {
                await DisplayAlert("提示", "未查询到生产作业指令卡详情。", "确定");
                return;
            }

            BindDetail(detail);
        }
        catch (Exception ex)
        {
            await DisplayAlert("查询失败", ex.Message, "确定");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private void BindDetail(WorkOrderDetailDto detail)
    {
        WorkOrderNoLabel.Text = ValueOrDash(detail.workOrderNo);
        OperationLabel.Text = ValueOrDash(detail.operationName);
        StatusLabel.Text = ValueOrDash(detail.workOrderStatus);
        CurrentOperationLabel.Text = ValueOrDash(detail.operationName);
        MachineLabel.Text = ValueOrDash(string.IsNullOrWhiteSpace(detail.machineNo) ? detail.deviceName : detail.machineNo);
        ProductLabel.Text = JoinNonEmpty(detail.steelGrade, detail.productSpecification, detail.materialProperty);
        MemoLabel.Text = detail.memo ?? string.Empty;

        BindMoldSequences(detail.moldSequenceList);
        var showProcessParams = !IsPicklingProcess(detail.operationName);
        ProcessParamsTitleLabel.IsVisible = showProcessParams;
        ProcessParamsBorder.IsVisible = showProcessParams;
        if (showProcessParams)
        {
            BindProcessParams(detail);
        }
        else
        {
            ProcessParamsGrid.Children.Clear();
            ProcessParamsGrid.RowDefinitions.Clear();
        }
    }

    private static bool IsPicklingProcess(string? operationName)
    {
        return operationName?.Contains("酸洗", StringComparison.OrdinalIgnoreCase) == true;
    }

    private void BindMoldSequences(IReadOnlyList<WorkOrderMoldSequenceDto>? sequences)
    {
        MoldSequenceLayout.Children.Clear();
        if (sequences is null || sequences.Count == 0)
        {
            MoldSequenceLayout.Children.Add(new Label { Text = "暂无模序要求", TextColor = Color.FromArgb("#5C6F8F") });
            return;
        }

        for (var i = 0; i < sequences.Count; i++)
        {
            var item = sequences[i];
            var card = new VerticalStackLayout { Spacing = 10 };
            card.Children.Add(new Label
            {
                Text = $"要求 {i + 1}",
                TextColor = Color.FromArgb("#C45A00"),
                FontAttributes = FontAttributes.Bold
            });
            card.Children.Add(BuildTwoColumnRow("生产重量(吨)", FormatDecimal(item.productionWeight), "件重(KG)", FormatDecimal(item.pieceWeight)));
            card.Children.Add(BuildSingleValueRow("生产件数", FormatDecimal(item.productionQuantity), Color.FromArgb("#00A651")));
            card.Children.Add(new Label { Text = "模序", TextColor = Color.FromArgb("#5C6F8F") });
            card.Children.Add(new Border
            {
                BackgroundColor = Color.FromArgb("#FFF4C9"),
                StrokeThickness = 0,
                Padding = 10,
                StrokeShape = new RoundRectangle { CornerRadius = 6 },
                Content = new Label
                {
                    Text = item.moldSequence,
                    TextColor = Color.FromArgb("#C45A00"),
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 15,
                    LineBreakMode = LineBreakMode.WordWrap
                }
            });

            MoldSequenceLayout.Children.Add(card);
            if (i < sequences.Count - 1)
            {
                MoldSequenceLayout.Children.Add(new BoxView
                {
                    HeightRequest = 1,
                    Color = Color.FromArgb("#FFD86B"),
                    Margin = new Thickness(0, 6)
                });
            }
        }
    }

    private void BindProcessParams(WorkOrderDetailDto detail)
    {
        ProcessParamsGrid.Children.Clear();
        ProcessParamsGrid.RowDefinitions.Clear();

        var items = new (string Label, string? Value)[]
        {
            ("质检方案编号", detail.inspectionSchemeCode),
            ("质检方案名称", detail.inspectionSchemeName),
            ("中间过程规格", detail.intermediateSpecification),
            ("炉号", detail.furnaceNo),
            ("投料规格", detail.inputSpecification),
            ("投料钢号", detail.inputSteelGrade),
            ("机台号", detail.machineNo),
            ("机台类型", detail.machineType),
            ("物料属性", detail.materialProperty),
            ("工序编码", detail.operationCode),
            ("工序名称", detail.operationName),
            ("其他要求", detail.otherRequirement),
            ("椭圆度控制", detail.ovalityControl),
            ("包装方式", detail.packageMethod),
            ("包装称重", detail.packageWeight),
            ("包装布颜色", detail.packagingClothColor),
            ("圈距控制", detail.pitchControl),
            ("成品规格", detail.productSpecification),
            ("质检单号", detail.qualityNo),
            ("销售方式", detail.saleMode),
            ("钢号", detail.steelGrade),
            ("收线长度(m)", detail.wireTakeUpLength),
            ("收线方式", detail.wireTakeUpMode),
            ("收线速度", detail.wireTakeUpSpeed),
            ("盘重要求", detail.coilWeightRequirement),
            ("圈径控制", detail.coilDiameterControl),
            ("拉拔方式", detail.drawMode),
            ("用途", detail.usagePurpose),
            ("开坯规格", detail.blankSpecification),
            ("开坯下公差(mm)", detail.billetLowerTolerance),
            ("开坯上公差(mm)", detail.billetUpperTolerance),
            ("DV(主线速度Hz)", detail.dvSpeed),
            ("生产件数", FormatDecimalOrFallback(detail.productionQuantity, FormatMoldSequenceTotal(detail.moldSequenceList, item => item.productionQuantity))),
            ("生产总重量(t)", FormatDecimalOrFallback(detail.productionWeight, FormatMoldSequenceTotal(detail.moldSequenceList, item => item.productionWeight))),
            ("是否打弯", FormatBool(detail.needBending)),
            ("是否需包装布", FormatBool(detail.needPackagingCloth)),
            ("是否打托", FormatBool(detail.needPalletizing)),
            ("是否磷化", FormatBool(detail.needPhosphating)),
            ("工单状态", detail.workOrderStatus)
        };
        var row = 0;
        for (var i = 0; i < items.Length; i += 2)
        {
            ProcessParamsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AddParamCell(row, 0, items[i].Label, items[i].Value);
            if (i + 1 < items.Length)
            {
                AddParamCell(row, 2, items[i + 1].Label, items[i + 1].Value);
            }
            row++;
        }
    }

    private void AddParamCell(int row, int column, string label, string? value)
    {
        ProcessParamsGrid.Add(new Label
        {
            Text = FormatParamLabel(label),
            TextColor = Color.FromArgb("#5C6F8F"),
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap,
            Margin = column == 2 ? new Thickness(14, 0, 0, 0) : Thickness.Zero
        }, column, row);
        ProcessParamsGrid.Add(new Label
        {
            Text = ProcessParamValueOrEmpty(value),
            TextColor = Colors.Black,
            FontAttributes = FontAttributes.Bold,
            FontSize = 13,
            HorizontalTextAlignment = TextAlignment.Start,
            LineBreakMode = LineBreakMode.WordWrap
        }, column + 1, row);
    }

    private static string FormatParamLabel(string label)
    {
        return label.Length > 4 ? $"{label[..4]}\n{label[4..]}" : label;
    }

    private static string? FormatDecimalOrFallback(decimal? value, string? fallback)
    {
        return value.HasValue ? FormatDecimal(value) : fallback;
    }

    private static string FormatBool(bool? value)
    {
        return value.HasValue ? (value.Value ? "是" : "否") : string.Empty;
    }

    private static string ProcessParamValueOrEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) || value.Equals("none", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : value;
    }

    private static string? FormatMoldSequenceTotal(
        IReadOnlyList<WorkOrderMoldSequenceDto>? sequences,
        Func<WorkOrderMoldSequenceDto, decimal?> selector)
    {
        if (sequences is null || sequences.Count == 0)
        {
            return null;
        }

        decimal total = 0;
        var hasValue = false;
        foreach (var sequence in sequences)
        {
            var value = selector(sequence);
            if (!value.HasValue)
            {
                continue;
            }

            total += value.Value;
            hasValue = true;
        }

        return hasValue ? FormatDecimal(total) : null;
    }

    private static Grid BuildTwoColumnRow(string label1, string value1, string label2, string value2)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };
        AddInlineValue(grid, 0, label1, value1, Colors.Black);
        AddInlineValue(grid, 2, label2, value2, Colors.Black);
        return grid;
    }

    private static Grid BuildSingleValueRow(string label, string value, Color valueColor)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };
        AddInlineValue(grid, 0, label, value, valueColor);
        return grid;
    }

    private static void AddInlineValue(Grid grid, int column, string label, string value, Color valueColor)
    {
        grid.Add(new Label { Text = label, TextColor = Color.FromArgb("#5C6F8F"), FontSize = 14 }, column, 0);
        grid.Add(new Label { Text = value, TextColor = valueColor, FontAttributes = FontAttributes.Bold, FontSize = 15, HorizontalTextAlignment = TextAlignment.End }, column + 1, 0);
    }

    private static string JoinNonEmpty(params string?[] values)
    {
        var text = string.Join(" ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(text) ? "--" : text;
    }

    private static string ValueOrDash(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "--" : value;
    }

    private static string FormatDecimal(decimal? value)
    {
        return value.HasValue ? value.Value.ToString("0.##") : "--";
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
