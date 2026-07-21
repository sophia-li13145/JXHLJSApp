using JXHLJSApp.Services;
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

            await BindDetailAsync(detail);
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "查询失败", ex.Message, "确定");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }


    private void ApplyStatusBadgeStyle(string? status)
    {
        var normalizedStatus = status?.Trim() ?? string.Empty;
        var isWaiting = normalizedStatus.Contains("待", StringComparison.OrdinalIgnoreCase)
            || normalizedStatus.Contains("未", StringComparison.OrdinalIgnoreCase)
            || normalizedStatus.Contains("wait", StringComparison.OrdinalIgnoreCase)
            || normalizedStatus.Contains("pending", StringComparison.OrdinalIgnoreCase);
        StatusBadgeBorder.BackgroundColor = Color.FromArgb(isWaiting ? "#FFF4E8" : "#EAF9EF");
        StatusLabel.TextColor = Color.FromArgb(isWaiting ? "#F08A00" : "#00A651");
    }

    private static string FormatOperationBadge(string? operationName)
    {
        var text = ValueOrDash(operationName);
        return text == "--" || text.EndsWith("工序", StringComparison.OrdinalIgnoreCase)
            ? text
            : $"{text}工序";
    }

    private async Task BindDetailAsync(WorkOrderDetailDto detail)
    {
        WorkOrderNoLabel.Text = ValueOrDash(detail.workOrderNo);
        OperationLabel.Text = FormatOperationBadge(detail.operationName);
        StatusLabel.Text = ValueOrDash(detail.workOrderStatus);
        ApplyStatusBadgeStyle(detail.workOrderStatus);
        CurrentOperationLabel.Text = ValueOrDash(detail.operationName);

        var processKind = GetProcessInstructionKind(detail);
        var isHeatTreatment = processKind == ProcessInstructionKind.HeatTreatment;
        MachineLabel.Text = await ResolveMachineDisplayAsync(detail, isHeatTreatment);
        ProductLabel.Text = JoinNonEmpty(detail.steelGrade, detail.productSpecification, detail.materialProperty);
        MemoLabel.Text = detail.memo ?? string.Empty;

        ProductInfoTitleLabel.IsVisible = !isHeatTreatment;
        ProductInfoDivider.IsVisible = !isHeatTreatment;
        ProductInfoGrid.IsVisible = !isHeatTreatment;
        MoldSequenceTitleLabel.IsVisible = !isHeatTreatment;
        MoldSequenceBorder.IsVisible = !isHeatTreatment;
        HeatTreatmentParamsLayout.IsVisible = isHeatTreatment;

        switch (processKind)
        {
            case ProcessInstructionKind.BlankOpening:
                BindBlankOpeningProductInfo(detail);
                MoldSequenceTitleLabel.Text = "生产信息";
                break;
            case ProcessInstructionKind.Drawing:
                BindDrawingProductInfo(detail);
                MoldSequenceTitleLabel.Text = "生产信息";
                break;
            default:
                BindDefaultProductInfo(detail);
                MoldSequenceTitleLabel.Text = "模序要求";
                break;
        }

        MoldSequenceBorder.BackgroundColor = Color.FromArgb("#F8FAFD");
        MoldSequenceBorder.Stroke = Color.FromArgb("#DDE6F1");

        if (isHeatTreatment)
        {
            BindHeatTreatmentParams(detail);
            ProcessParamsTitleLabel.IsVisible = false;
            ProcessParamsBorder.IsVisible = false;
            ProcessParamsGrid.Children.Clear();
            ProcessParamsGrid.RowDefinitions.Clear();
            return;
        }

        if (processKind == ProcessInstructionKind.BlankOpening || processKind == ProcessInstructionKind.Drawing)
        {
            BindPrimaryProductionInfo(detail, processKind);
        }
        else
        {
            BindMoldSequences(detail.moldSequenceList, processKind);
        }
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

    private async Task<string> ResolveMachineDisplayAsync(WorkOrderDetailDto detail, bool isHeatTreatment)
    {
        if (!isHeatTreatment)
        {
            return ValueOrDash(string.IsNullOrWhiteSpace(detail.machineNo) ? detail.deviceName : detail.machineNo);
        }

        var machineCodeCandidates = new[] { detail.machineNo, detail.deviceCode, detail.deviceName }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToArray();
        if (machineCodeCandidates.Length == 0)
        {
            return "--";
        }

        var devices = await _workOrderApi.GetDeviceListAsync();
        var device = devices.FirstOrDefault(item => machineCodeCandidates.Any(candidate =>
            string.Equals(item.devCode, candidate, StringComparison.OrdinalIgnoreCase)));
        return ValueOrDash(FirstNonEmptyOrDefault(device?.devName, detail.deviceName, detail.machineNo, detail.deviceCode));
    }

    private void BindDefaultProductInfo(WorkOrderDetailDto detail)
    {
        ProductInfoGrid.Children.Clear();
        ProductInfoGrid.RowDefinitions.Clear();
        ProductInfoGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        ProductInfoGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        AddProductInfoCell(0, 0, "当前工序", ValueOrDash(detail.operationName));
        AddProductInfoCell(0, 2, "所属机台", ValueOrDash(string.IsNullOrWhiteSpace(detail.machineNo) ? detail.deviceName : detail.machineNo));
        AddProductInfoCell(1, 0, "成品要求", JoinNonEmpty(detail.steelGrade, detail.productSpecification, detail.materialProperty), 3);
    }

    private void BindBlankOpeningProductInfo(WorkOrderDetailDto detail)
    {
        ProductInfoGrid.Children.Clear();
        ProductInfoGrid.RowDefinitions.Clear();
        ProductInfoGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        ProductInfoGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        ProductInfoGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        AddProductInfoCell(0, 0, "聚合单号", FirstNonEmpty(detail.qualityNo, detail.workOrderNo), 3);
        AddProductInfoCell(1, 0, "机台类型", ValueOrDash(detail.machineType));
        AddProductInfoCell(1, 2, "机台号", ValueOrDash(FirstNonEmpty(detail.machineNo, detail.deviceName, detail.deviceCode)));
        AddProductInfoCell(2, 0, "件重(KG)", FormatDecimalOrFallback(detail.pieceWeight, FormatDecimalOrFallback(FirstMoldSequenceValue(detail.moldSequenceList, item => item.pieceWeight), null)));
        AddProductInfoCell(2, 2, "拉拔方式", FirstNonEmpty(detail.drawMode, detail.wireTakeUpMode));
    }


    private void BindDrawingProductInfo(WorkOrderDetailDto detail)
    {
        ProductInfoGrid.Children.Clear();
        ProductInfoGrid.RowDefinitions.Clear();
        for (var i = 0; i < 6; i++)
        {
            ProductInfoGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        AddProductInfoCell(0, 0, "聚合单号", FirstNonEmpty(detail.qualityNo, detail.workOrderNo), 3);
        AddProductInfoCell(1, 0, "机台类型", ValueOrDash(detail.machineType));
        AddProductInfoCell(1, 2, "机台号", ValueOrDash(FirstNonEmpty(detail.machineNo, detail.deviceName, detail.deviceCode)));
        AddProductInfoCell(2, 0, "挂牌", ValueOrDash(detail.steelGrade));
        AddProductInfoCell(2, 2, "下料规格", ValueOrDash(detail.inputSpecification));
        AddProductInfoCell(3, 0, "生产", ValueOrDash(detail.materialProperty));
        AddProductInfoCell(3, 2, "拉拔方式", ValueOrDash(detail.drawMode));
        AddProductInfoCell(4, 0, "圈径", ValueOrDash(detail.coilDiameterControl));
        AddProductInfoCell(4, 2, "件重(KG)", FormatDecimalOrFallback(detail.pieceWeight, FormatDecimalOrFallback(FirstMoldSequenceValue(detail.moldSequenceList, item => item.pieceWeight), null)));
        AddProductInfoCell(5, 0, "包装", ValueOrDash(detail.packageMethod));
        AddProductInfoCell(5, 2, "客户代码", ValueOrDash(detail.customerCode));
    }

    private void AddProductInfoCell(int row, int column, string label, string? value, int valueColumnSpan = 1)
    {
        ProductInfoGrid.Add(new Label
        {
            Text = label,
            TextColor = Color.FromArgb("#5C6F8F"),
            FontSize = 13,
            VerticalTextAlignment = TextAlignment.Center
        }, column, row);
        var valueLabel = new Label
        {
            Text = ProcessParamValueOrEmpty(value),
            TextColor = Colors.Black,
            FontAttributes = FontAttributes.Bold,
            FontSize = 13,
            HorizontalTextAlignment = TextAlignment.End,
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap
        };
        ProductInfoGrid.Add(valueLabel, column + 1, row);
        if (valueColumnSpan > 1)
        {
            Grid.SetColumnSpan(valueLabel, valueColumnSpan);
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

    private static ProcessInstructionKind GetProcessInstructionKind(WorkOrderDetailDto detail)
    {
        var normalizedOperationName = detail.operationName?.Trim() ?? string.Empty;
        var normalizedOperationCode = detail.operationCode?.Trim() ?? string.Empty;
        var normalizedWorkOrderNo = detail.workOrderNo?.Trim() ?? string.Empty;

        if (ContainsAny(normalizedOperationName, "开坯")
            || string.Equals(normalizedOperationCode, "KP", StringComparison.OrdinalIgnoreCase)
            || normalizedWorkOrderNo.Contains("-KP-", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessInstructionKind.BlankOpening;
        }

        if (ContainsAny(normalizedOperationName, "拉拔")
            || string.Equals(normalizedOperationCode, "LB", StringComparison.OrdinalIgnoreCase)
            || normalizedOperationCode.Contains("draw", StringComparison.OrdinalIgnoreCase)
            || normalizedWorkOrderNo.Contains("-LB-", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessInstructionKind.Drawing;
        }

        if (ContainsAny(normalizedOperationName, "热处理")
            || string.Equals(normalizedOperationCode, "RL", StringComparison.OrdinalIgnoreCase)
            || normalizedOperationCode.Contains("heat", StringComparison.OrdinalIgnoreCase)
            || normalizedWorkOrderNo.Contains("-RL-", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessInstructionKind.HeatTreatment;
        }

        return ProcessInstructionKind.Default;
    }

    private static bool ContainsAny(string value, params string[] keywords)
    {
        return keywords.Any(keyword => value.Contains(keyword, StringComparison.OrdinalIgnoreCase));
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


    private void BindPrimaryProductionInfo(WorkOrderDetailDto detail, ProcessInstructionKind processKind)
    {
        MoldSequenceLayout.Children.Clear();
        var firstSequence = detail.moldSequenceList?.FirstOrDefault();
        var displayItem = new WorkOrderMoldSequenceDto
        {
            moldSequence = JoinMoldSequences(detail.moldSequenceList),
            pieceWeight = detail.pieceWeight ?? firstSequence?.pieceWeight,
            productionQuantity = detail.productionQuantity ?? firstSequence?.productionQuantity,
            productionWeight = detail.productionWeight ?? firstSequence?.productionWeight
        };

        MoldSequenceLayout.Children.Add(BuildPrimaryProductionCard(displayItem, processKind));
    }

    private static string? JoinMoldSequences(IReadOnlyList<WorkOrderMoldSequenceDto>? sequences)
    {
        if (sequences is null || sequences.Count == 0)
        {
            return null;
        }

        var text = string.Join("-", sequences
            .Select(item => item.moldSequence?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(text) ? null : text;
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
                return BuildDrawingMoldSequenceCard(item);
            default:
                card.Children.Add(BuildTwoColumnRow("生产重量(吨)", FormatDecimal(item.productionWeight), "件重(KG)", FormatDecimal(item.pieceWeight)));
                card.Children.Add(BuildSingleValueRow("生产件数", FormatDecimal(item.productionQuantity), Color.FromArgb("#00A651")));
                break;
        }
        card.Children.Add(new Label { Text = "模序", TextColor = Color.FromArgb("#5C6F8F") });
        card.Children.Add(BuildMoldSequenceTextBorder(item.moldSequence, Color.FromArgb("#FFF4C9"), Color.FromArgb("#C45A00"), new Thickness(10)));

        return card;
    }

    private static VerticalStackLayout BuildDrawingMoldSequenceCard(WorkOrderMoldSequenceDto item) =>
        BuildPrimaryProductionCard(item, ProcessInstructionKind.Drawing);

    private static VerticalStackLayout BuildPrimaryProductionCard(WorkOrderMoldSequenceDto item, ProcessInstructionKind processKind)
    {
        var weightLabel = processKind == ProcessInstructionKind.BlankOpening ? "生产总重量(吨)" : "生产重量(吨)";
        var card = new VerticalStackLayout { Spacing = 10 };
        card.Children.Add(BuildTwoColumnRow(weightLabel, FormatDecimal(item.productionWeight), "生产件数", FormatDecimal(item.productionQuantity)));
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

        if (processKind == ProcessInstructionKind.Drawing)
        {
            BindDrawingProcessParams(detail);
            return;
        }

        if (processKind == ProcessInstructionKind.BlankOpening)
        {
            BindBlankOpeningProcessParams(detail);
            return;
        }

        ProcessParamsTitleLabel.IsVisible = true;
        ProcessParamsTitleLabel.Text = "⚙️ 工艺基础参数";
        ProcessParamsBorder.BackgroundColor = Color.FromArgb("#F8FAFD");
        ProcessParamsBorder.Stroke = Color.FromArgb("#DDE6F1");
        ProcessParamsBorder.StrokeThickness = 1;
        ProcessParamsBorder.Padding = new Thickness(12);
        ProcessParamsGrid.RowSpacing = 14;

        var items = processKind switch
        {
            ProcessInstructionKind.BlankOpening => BuildBlankOpeningParams(detail),
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

    private void BindDrawingProcessParams(WorkOrderDetailDto detail)
    {
        ProcessParamsTitleLabel.IsVisible = false;
        ProcessParamsBorder.BackgroundColor = Colors.Transparent;
        ProcessParamsBorder.StrokeThickness = 0;
        ProcessParamsBorder.Padding = Thickness.Zero;
        ProcessParamsGrid.RowSpacing = 16;

        var row = 0;
        AddDrawingFullWidthParamRow(row++, "收线速度", detail.wireTakeUpSpeed);
        AddDrawingTwoParamRow(row++, "收线方式", detail.wireTakeUpMode, "钢丝形状", detail.steelWireShape);
        AddDrawingFullWidthParamRow(row++, "收线长度", FormatLengthWithUnit(detail.wireTakeUpLength));
        AddDrawingFullWidthParamRow(row++, "盘重要求", detail.coilWeightRequirement);
        AddDrawingFullWidthParamRow(row++, "产品直径", detail.productSpecification);
        AddDrawingTwoParamRow(row++, "下公差(mm)", detail.billetLowerTolerance, "上公差(mm)", detail.billetUpperTolerance);
        AddDrawingTwoParamRow(row++, "圈距控制", detail.pitchControl, "圈径控制", detail.coilDiameterControl);
        AddDrawingTwoParamRow(row, "椭圆度控制", detail.ovalityControl, "质检方式", detail.inspectionSchemeName);
    }

    private void BindBlankOpeningProcessParams(WorkOrderDetailDto detail)
    {
        ProcessParamsTitleLabel.IsVisible = false;
        ProcessParamsBorder.BackgroundColor = Colors.Transparent;
        ProcessParamsBorder.StrokeThickness = 0;
        ProcessParamsBorder.Padding = Thickness.Zero;
        ProcessParamsGrid.RowSpacing = 16;

        var row = 0;
        AddDrawingTwoParamRow(row++, "收线速度", detail.wireTakeUpSpeed, "收线方式", detail.wireTakeUpMode);
        AddDrawingFullWidthParamRow(row++, "炉号", detail.furnaceNo);
        AddDrawingFullWidthParamRow(row++, "收线长度", FormatLengthWithUnit(detail.wireTakeUpLength));
        AddDrawingFullWidthParamRow(row++, "盘重要求", detail.coilWeightRequirement);
        AddDrawingTwoParamRow(row++, "投料钢号", detail.inputSteelGrade, "上料规格", detail.inputSpecification);
        AddDrawingTwoParamRow(row++, "钢号", detail.steelGrade, "下料规格", detail.blankSpecification);
        AddDrawingTwoParamRow(row++, "开坯下公差(mm)", detail.billetLowerTolerance, "开坯上公差(mm)", detail.billetUpperTolerance);
        AddDrawingTwoParamRow(row++, "圈距控制", detail.pitchControl, "圈径控制", detail.coilDiameterControl);
        AddDrawingTwoParamRow(row++, "椭圆度控制", detail.ovalityControl, "质检方式", detail.inspectionSchemeName);
        AddDrawingSeparator(row++);
        AddDrawingFullWidthParamRow(row, "用途", detail.usagePurpose);
    }

    private void AddDrawingSeparator(int row)
    {
        ProcessParamsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        var separator = new BoxView
        {
            HeightRequest = 1,
            Color = Color.FromArgb("#D8E0EA"),
            Margin = new Thickness(0, 2)
        };
        ProcessParamsGrid.Add(separator, 0, row);
        Grid.SetColumnSpan(separator, 4);
    }

    private void AddDrawingFullWidthParamRow(int row, string label, string? value)
    {
        ProcessParamsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        AddDrawingParamLabel(row, 0, label, Thickness.Zero);
        var valueLabel = BuildDrawingParamValue(value);
        ProcessParamsGrid.Add(valueLabel, 1, row);
        Grid.SetColumnSpan(valueLabel, 3);
    }

    private void AddDrawingTwoParamRow(int row, string leftLabel, string? leftValue, string rightLabel, string? rightValue)
    {
        ProcessParamsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        AddDrawingParamLabel(row, 0, leftLabel, Thickness.Zero);
        ProcessParamsGrid.Add(BuildDrawingParamValue(leftValue), 1, row);
        AddDrawingParamLabel(row, 2, rightLabel, new Thickness(14, 0, 0, 0));
        ProcessParamsGrid.Add(BuildDrawingParamValue(rightValue), 3, row);
    }

    private void AddDrawingParamLabel(int row, int column, string label, Thickness margin)
    {
        ProcessParamsGrid.Add(new Label
        {
            Text = label,
            TextColor = Color.FromArgb("#5C6F8F"),
            FontSize = 13,
            LineBreakMode = LineBreakMode.NoWrap,
            Margin = margin
        }, column, row);
    }

    private static Label BuildDrawingParamValue(string? value)
    {
        return new Label
        {
            Text = ProcessParamValueOrEmpty(value),
            TextColor = Colors.Black,
            FontAttributes = FontAttributes.Bold,
            FontSize = 13,
            HorizontalTextAlignment = TextAlignment.End,
            LineBreakMode = LineBreakMode.WordWrap
        };
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
            ("质检方式", detail.inspectionSchemeName),
            ("用途", detail.usagePurpose)
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

    private static decimal? FirstMoldSequenceValue(
        IReadOnlyList<WorkOrderMoldSequenceDto>? sequences,
        Func<WorkOrderMoldSequenceDto, decimal?> selector)
    {
        return sequences?.Select(selector).FirstOrDefault(value => value.HasValue);
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

    private static string? FirstNonEmptyOrDefault(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
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
