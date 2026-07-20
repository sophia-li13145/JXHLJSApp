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

        var processKind = GetProcessInstructionKind(detail.operationName);
        var isHeatTreatment = processKind == ProcessInstructionKind.HeatTreatment;
        ProductInfoGrid.IsVisible = !isHeatTreatment;
        MoldSequenceTitleLabel.IsVisible = !isHeatTreatment;
        MoldSequenceBorder.IsVisible = !isHeatTreatment;
        HeatTreatmentParamsLayout.IsVisible = isHeatTreatment;

        if (isHeatTreatment)
        {
            BindHeatTreatmentParams(detail);
            ProcessParamsTitleLabel.IsVisible = false;
            ProcessParamsBorder.IsVisible = false;
            ProcessParamsGrid.Children.Clear();
            ProcessParamsGrid.RowDefinitions.Clear();
            return;
        }

        BindMoldSequences(detail.moldSequenceList, processKind);
        var showProcessParams = !IsPicklingProcess(detail.operationName);
        ProcessParamsTitleLabel.IsVisible = showProcessParams;
        ProcessParamsBorder.IsVisible = showProcessParams;
        if (showProcessParams)
        {
            BindProcessParams(detail, processKind);
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

    private enum ProcessInstructionKind
    {
        Default,
        BlankOpening,
        Drawing,
        HeatTreatment
    }

    private static ProcessInstructionKind GetProcessInstructionKind(string? operationName)
    {
        var normalizedOperationName = operationName?.Trim();
        if (string.Equals(normalizedOperationName, "开胚", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedOperationName, "开坯", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessInstructionKind.BlankOpening;
        }

        if (string.Equals(normalizedOperationName, "拉拔", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessInstructionKind.Drawing;
        }

        return string.Equals(normalizedOperationName, "热处理", StringComparison.OrdinalIgnoreCase)
            ? ProcessInstructionKind.HeatTreatment
            : ProcessInstructionKind.Default;
    }

    private void BindHeatTreatmentParams(WorkOrderDetailDto detail)
    {
        HeatTreatmentParamsLayout.Children.Clear();
        var items = BuildHeatTreatmentParams(detail);
        for (var i = 0; i < items.Length; i++)
        {
            HeatTreatmentParamsLayout.Children.Add(BuildHeatTreatmentParamRow(items[i].Label, items[i].Value));
            if (i < items.Length - 1)
            {
                HeatTreatmentParamsLayout.Children.Add(new BoxView
                {
                    HeightRequest = 1,
                    Color = Color.FromArgb("#E8EDF5")
                });
            }
        }
    }

    private static (string Label, string? Value)[] BuildHeatTreatmentParams(WorkOrderDetailDto detail)
    {
        return new (string Label, string? Value)[]
        {
            ("钢号", detail.steelGrade),
            ("规格", detail.productSpecification),
            ("DV", FormatDvWithUnit(detail.dvSpeed)),
            ("生产批号", FirstNonEmpty(detail.productionBatchNo, detail.productionBatch, detail.batchNo, detail.qualityNo)),
            ("销售方式", detail.saleMode),
            ("是否打弯", FormatBool(detail.needBending)),
            ("是否磷化", FormatBool(detail.needPhosphating)),
            ("日期", FormatCompactDate(detail.productionDate)),
            ("机台", FirstNonEmpty(detail.machineNo, detail.deviceName, detail.deviceCode)),
            ("班次", FirstNonEmpty(detail.shiftName, detail.shiftNo))
        };
    }

    private static Grid BuildHeatTreatmentParamRow(string label, string? value)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            Padding = new Thickness(0, 13),
            ColumnSpacing = 12
        };
        grid.Add(new Label
        {
            Text = label,
            TextColor = Color.FromArgb("#5C6F8F"),
            FontSize = 14,
            VerticalTextAlignment = TextAlignment.Center
        }, 0, 0);
        grid.Add(new Label
        {
            Text = ProcessParamValueOrEmpty(value),
            TextColor = Colors.Black,
            FontAttributes = FontAttributes.Bold,
            FontSize = 14,
            HorizontalTextAlignment = TextAlignment.End,
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap
        }, 1, 0);

        return grid;
    }

    private void BindMoldSequences(IReadOnlyList<WorkOrderMoldSequenceDto>? sequences, ProcessInstructionKind processKind)
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
            var card = processKind == ProcessInstructionKind.Drawing
                ? BuildDrawingMoldSequenceCard(item)
                : BuildDefaultMoldSequenceCard(item, i, processKind);

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

    private static VerticalStackLayout BuildDefaultMoldSequenceCard(WorkOrderMoldSequenceDto item, int index, ProcessInstructionKind processKind)
    {
        var card = new VerticalStackLayout { Spacing = 10 };
        card.Children.Add(new Label
        {
            Text = $"要求 {index + 1}",
            TextColor = Color.FromArgb("#C45A00"),
            FontAttributes = FontAttributes.Bold
        });
        switch (processKind)
        {
            case ProcessInstructionKind.BlankOpening:
                card.Children.Add(BuildSingleValueRow("生产重量(吨)", FormatDecimal(item.productionWeight), Colors.Black));
                card.Children.Add(BuildSingleValueRow("生产产数", FormatDecimal(item.productionQuantity), Color.FromArgb("#00A651")));
                break;
            default:
                card.Children.Add(BuildTwoColumnRow("生产重量(吨)", FormatDecimal(item.productionWeight), "件重(KG)", FormatDecimal(item.pieceWeight)));
                card.Children.Add(BuildSingleValueRow("生产件数", FormatDecimal(item.productionQuantity), Color.FromArgb("#00A651")));
                break;
        }
        card.Children.Add(new Label { Text = "模序", TextColor = Color.FromArgb("#5C6F8F") });
        card.Children.Add(BuildMoldSequenceTextBorder(item.moldSequence, Color.FromArgb("#FFF4C9"), Color.FromArgb("#C45A00"), new Thickness(10)));

        return card;
    }

    private static VerticalStackLayout BuildDrawingMoldSequenceCard(WorkOrderMoldSequenceDto item)
    {
        var card = new VerticalStackLayout { Spacing = 10 };
        card.Children.Add(BuildTwoColumnRow("生产重量(吨)", FormatDecimal(item.productionWeight), "生产件数", FormatDecimal(item.productionQuantity)));
        card.Children.Add(new BoxView
        {
            HeightRequest = 1,
            Color = Color.FromArgb("#DDE6F1"),
            Margin = new Thickness(0, 2)
        });
        card.Children.Add(new Label
        {
            Text = "模序",
            TextColor = Colors.Black,
            FontAttributes = FontAttributes.Bold
        });
        card.Children.Add(BuildMoldSequenceTextBorder(item.moldSequence, Colors.Transparent, Colors.Black, Thickness.Zero));

        return card;
    }

    private static Border BuildMoldSequenceTextBorder(string? moldSequence, Color backgroundColor, Color textColor, Thickness padding)
    {
        return new Border
        {
            BackgroundColor = backgroundColor,
            StrokeThickness = 0,
            Padding = padding,
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            Content = new Label
            {
                Text = ValueOrDash(moldSequence),
                TextColor = textColor,
                FontAttributes = FontAttributes.Bold,
                FontSize = 15,
                LineBreakMode = LineBreakMode.WordWrap
            }
        };
    }

    private void BindProcessParams(WorkOrderDetailDto detail, ProcessInstructionKind processKind)
    {
        ProcessParamsGrid.Children.Clear();
        ProcessParamsGrid.RowDefinitions.Clear();

        var items = processKind switch
        {
            ProcessInstructionKind.BlankOpening => BuildBlankOpeningParams(detail),
            ProcessInstructionKind.Drawing => BuildDrawingParams(detail),
            _ => BuildDefaultProcessParams(detail)
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

    private static (string Label, string? Value)[] BuildBlankOpeningParams(WorkOrderDetailDto detail)
    {
        return new (string Label, string? Value)[]
        {
            ("收线速度", detail.wireTakeUpSpeed),
            ("收线方式", detail.wireTakeUpMode),
            ("炉号", detail.furnaceNo),
            ("收线长度", FormatLengthWithUnit(detail.wireTakeUpLength)),
            ("盘重要求", detail.coilWeightRequirement),
            ("投料钢号", detail.inputSteelGrade),
            ("投料规格", detail.inputSpecification),
            ("钢号", detail.steelGrade),
            ("开坯规格", detail.blankSpecification),
            ("开坯下公差(mm)", detail.billetLowerTolerance),
            ("开坯上公差(mm)", detail.billetUpperTolerance),
            ("圈距控制", detail.pitchControl),
            ("圈径控制", detail.coilDiameterControl),
            ("椭圆度控制", detail.ovalityControl),
            ("质检方式", detail.inspectionSchemeName)
        };
    }

    private static (string Label, string? Value)[] BuildDrawingParams(WorkOrderDetailDto detail)
    {
        return new (string Label, string? Value)[]
        {
            ("收线速度", detail.wireTakeUpSpeed),
            ("收线方式", detail.wireTakeUpMode),
            ("钢丝形状", detail.materialProperty),
            ("收线长度", FormatLengthWithUnit(detail.wireTakeUpLength)),
            ("盘重要求", detail.coilWeightRequirement),
            ("产品直径", detail.productSpecification),
            ("下公差(mm)", detail.billetLowerTolerance),
            ("上公差(mm)", detail.billetUpperTolerance),
            ("圈距控制", detail.pitchControl),
            ("圈径控制", detail.coilDiameterControl),
            ("椭圆度控制", detail.ovalityControl),
            ("质检方式", detail.inspectionSchemeName)
        };
    }

    private static (string Label, string? Value)[] BuildDefaultProcessParams(WorkOrderDetailDto detail)
    {
        return new (string Label, string? Value)[]
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
        return label;
    }

    private static string? FormatDecimalOrFallback(decimal? value, string? fallback)
    {
        return value.HasValue ? FormatDecimal(value) : fallback;
    }

    private static string FormatLengthWithUnit(string? value)
    {
        var text = ProcessParamValueOrEmpty(value);
        return string.IsNullOrWhiteSpace(text) || text.Contains("米", StringComparison.OrdinalIgnoreCase) || text.Contains("m", StringComparison.OrdinalIgnoreCase)
            ? text
            : $"{text}米";
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static string FormatDvWithUnit(string? value)
    {
        var text = ProcessParamValueOrEmpty(value);
        return string.IsNullOrWhiteSpace(text) || text.Contains("hz", StringComparison.OrdinalIgnoreCase)
            ? text
            : $"{text}Hz";
    }

    private static string FormatCompactDate(string? value)
    {
        var text = ProcessParamValueOrEmpty(value);
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return DateTime.TryParse(text, out var date)
            ? date.ToString("yyyyMMdd")
            : text.Replace("-", string.Empty).Replace("/", string.Empty);
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
