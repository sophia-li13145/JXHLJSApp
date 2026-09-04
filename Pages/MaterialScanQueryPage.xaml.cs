using JXHLJSApp.Models;
using JXHLJSApp.Services;
using Microsoft.Maui.Controls.Shapes;
using System.Globalization;

namespace JXHLJSApp.Pages;

public partial class MaterialScanQueryPage : ContentPage
{
    private readonly IMaterialScanQueryApi _api;
    private readonly IScanService _scanService;
    private bool _scanStarted;

    public MaterialScanQueryPage(IMaterialScanQueryApi api, IScanService scanService)
    {
        InitializeComponent();
        _api = api;
        _scanService = scanService;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_scanStarted)
        {
            _scanStarted = true;
            _ = ScanAndQueryAsync();
        }
    }

    private async Task ScanAndQueryAsync()
    {
        var code = await _scanService.ScanAsync("扫描物料二维码");
        if (string.IsNullOrWhiteSpace(code)) return;

        try
        {
            SetLoading(true);
            Render(await _api.QueryAsync(code));
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "查询失败", ex.Message, "确定");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void Render(MaterialScanQueryDto result)
    {
        var state = result.basicInfo?.materialState?.Trim().ToLowerInvariant() ?? "unknown";
        TitleLabel.Text = state switch
        {
            "raw" => "原料扫码结果",
            "pickled" => "酸洗扫码结果",
            "heat_treated" => "热处理扫码结果",
            "drawn" => "拉拔扫码结果",
            "finished" => "成品扫码结果",
            "bloomed" => "开坯扫码结果",
            _ => "扫码查询结果"
        };

        ResultStack.Children.Clear();
        ResultStack.Children.Add(CreateSection("基本信息", BasicFields(
            result.basicInfo,
            showState: state != "raw",
            showPackagePieceWeight: state == "finished")));

        if (state == "bloomed")
        {
            ResultStack.Children.Add(CreateSection("生产指令卡", BloomedInstructionFields(result.instructionCardInfo), 2));
            ResultStack.Children.Add(CreateSection("实测收线长度与重量", BloomedOutputFields(result.instructionCardInfo), 2));
            ResultStack.Children.Add(CreateSection("质检内容", BloomedInspectionFields(result.inspectionInfo), 2));
        }
        else if (state == "heat_treated")
        {
            ResultStack.Children.Add(CreateSection("生产指令卡", HeatInstructionFields(result.instructionCardInfo), 2));
            ResultStack.Children.Add(CreateSection("质检内容", HeatInspectionFields(result.inspectionInfo), 2));
        }
        else if (state is "drawn" or "finished")
        {
            var actualPieceWeight = state == "finished"
                ? result.basicInfo?.pieceWeight
                : result.instructionCardInfo?.pieceWeight;
            ResultStack.Children.Add(CreateSection("生产指令卡", DrawnInstructionFields(result.instructionCardInfo, actualPieceWeight), 2));
            ResultStack.Children.Add(CreateSection("质检内容", DrawnInspectionFields(result.inspectionInfo), 2));
        }
    }

    private static IReadOnlyList<(string Label, object? Value)> BasicFields(
        MaterialScanBasicInfoDto? x,
        bool showState,
        bool showPackagePieceWeight)
    {
        var fields = new List<(string, object?)>
        {
            ("物料名称", x?.materialName), ("规格", x?.spec), ("炉号", x?.furnaceNo),
            ("产地", x?.origin), ("件重", FormatUnit(x?.pieceWeight, "KG", "千克", "公斤"))
        };

        if (showState) fields.Add(("物料状态", StateName(x?.materialState)));
        if (showPackagePieceWeight) fields.Add(("包装件重", FormatUnit(x?.packagePieceWeight, "KG", "千克", "公斤")));
        return fields;
    }

    private static IReadOnlyList<(string, object?)> BloomedInstructionFields(MaterialScanInstructionCardInfoDto? x) =>
        new (string, object?)[]
        {
            ("上料规格", x?.inputSpecification), ("下料规格", x?.blankSpecification), ("生/淬", x?.rawOrQuench), ("扭转（次）", FormatTorsion(x?.torsion)),
            ("上公差", FormatUnit(x?.billetUpperTolerance, "mm")), ("下公差", FormatLowerTolerance(x?.billetLowerTolerance)), ("客户代码", x?.customerIdentifier), ("拉拔方式", x?.drawMode),
            ("收线速度", FormatUnit(x?.wireTakeUpSpeed, x?.wireTakeUpSpeedUnit)), ("钢丝形状", x?.wireShape), ("收线长度", FormatUnit(x?.wireTakeUpLength, "m")), ("椭圆度控制", FormatUpperLimit(x?.ovalityControl)),
            ("圈径控制", FormatUnit(x?.coilDiameterControl, "mm")), ("圈距控制", FormatUpperLimit(x?.coilPitchControl)),
            ("强度范围", FormatUnit(x?.strengthRange, "MPa")), ("件重", FormatUnit(x?.pieceWeight, "KG", "千克", "公斤"))
        };

    private static IReadOnlyList<(string, object?)> BloomedOutputFields(MaterialScanInstructionCardInfoDto? x) =>
        new (string, object?)[]
        {
            ("收线长度", FormatUnit(x?.outputLength, "m")),
            ("件重", FormatUnit(x?.outputWeight, "KG", "千克", "公斤"))
        };

    private static IReadOnlyList<(string, object?)> BloomedInspectionFields(MaterialScanInspectionInfoDto? x) =>
        new (string, object?)[]
        {
            ("圈径控制", FormatUnit(x?.coilDiameterControl, "mm")), ("圈距控制", FormatUpperLimit(x?.coilPitchControl)), ("实测直径", FormatUnit(x?.actualDiameterMm, "mm")), ("表面", x?.surfaceCondition),
            ("是否合格", BoolText(x?.isQualified)), ("不合格说明", x?.unqualifiedDescription), ("是否连续性不合格品", BoolText(x?.continuouslyUnqualified)),
            ("是否员工干预", BoolText(x?.employeeIntervention)), ("备注", x?.memo), ("检验员", x?.inspector), ("检验日期", x?.inspectDate)
        };

    private static IReadOnlyList<(string, object?)> HeatInstructionFields(MaterialScanInstructionCardInfoDto? x) =>
        new (string, object?)[] { ("DV", FormatUnit(x?.dvSpeed, "Hz")), ("销售方式", x?.saleMode) };

    private static IReadOnlyList<(string, object?)> HeatInspectionFields(MaterialScanInspectionInfoDto? x) =>
        new (string, object?)[]
        {
            ("实测直径", FormatUnit(x?.actualDiameterMm, "mm")), ("实测强度", FormatUnit(x?.strengthMpa, "MPa")), ("扭转", FormatTorsion(x?.torsion)), ("断后直径", FormatUnit(x?.brokenDiameter, "mm")),
            ("断面收缩率", FormatUnit(x?.reductionOfAreaRate, "%")), ("延伸率", FormatUnit(x?.elongationRate, "%")), ("备注", x?.memo), ("检验员", x?.inspector), ("检验日期", x?.inspectDate)
        };

    private static IReadOnlyList<(string, object?)> DrawnInstructionFields(MaterialScanInstructionCardInfoDto? x, decimal? actualPieceWeight) =>
        new (string, object?)[]
        {
            ("上料规格", x?.inputSpecification), ("下料规格", x?.blankSpecification), ("强度", FormatUnit(x?.strengthRange, "MPa")), ("生/淬", x?.rawOrQuench),
            ("扭转（次）", FormatTorsion(x?.torsion)), ("上公差", FormatUnit(x?.billetUpperTolerance, "mm")), ("下公差", FormatLowerTolerance(x?.billetLowerTolerance)), ("客户代码", x?.customerIdentifier),
            ("拉拔方式", x?.drawMode), ("收线速度", FormatUnit(x?.wireTakeUpSpeed, x?.wireTakeUpSpeedUnit)), ("钢丝形状", x?.wireShape), ("收线长度", FormatUnit(x?.wireTakeUpLength, "m")),
            ("椭圆度控制", FormatUpperLimit(x?.ovalityControl)), ("圈径控制", FormatUnit(x?.coilDiameterControl, "mm")), ("圈距控制", FormatUpperLimit(x?.coilPitchControl)), ("实测收线长度", FormatUnit(x?.outputLength, "m")),
            ("实测收线重量", FormatUnit(x?.outputWeight, "KG", "千克", "公斤")), ("件重", FormatUnit(actualPieceWeight, "KG", "千克", "公斤"))
        };

    private static IReadOnlyList<(string, object?)> DrawnInspectionFields(MaterialScanInspectionInfoDto? x) =>
        new (string, object?)[]
        {
            ("圈径控制", FormatUnit(x?.coilDiameterControl, "mm")), ("圈距控制", FormatUpperLimit(x?.coilPitchControl)), ("实测直径", FormatUnit(x?.actualDiameterMm, "mm")), ("强度", FormatUnit(x?.strengthMpa, "MPa")),
            ("扭转", FormatTorsion(x?.torsion)), ("表面", x?.surfaceCondition), ("延伸率", FormatUnit(x?.elongationRate, "%")), ("是否合格", BoolText(x?.isQualified)),
            ("不合格说明", x?.unqualifiedDescription), ("是否连续性不合格品", BoolText(x?.continuouslyUnqualified)),
            ("是否员工干预", BoolText(x?.employeeIntervention)), ("备注", x?.memo), ("检验员", x?.inspector), ("检验日期", x?.inspectDate)
        };

    private static View CreateSection(string title, IReadOnlyList<(string Label, object? Value)> fields, int columns = 1)
    {
        var grid = new Grid { Padding = 14, ColumnSpacing = 14, RowSpacing = 13, BackgroundColor = Colors.White };
        for (var i = 0; i < columns; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        var column = 0;
        var row = 0;
        foreach (var field in fields)
        {
            var isInspectionDate = field.Label == "检验日期";
            if (isInspectionDate && column != 0)
            {
                row++;
                column = 0;
            }

            var label = new Label
            {
                Text = $"{field.Label}：{ValueText(field.Value)}",
                FontSize = 14,
                TextColor = Colors.Black,
                LineBreakMode = isInspectionDate ? LineBreakMode.NoWrap : LineBreakMode.WordWrap
            };
            grid.Add(label, column, row);

            if (isInspectionDate)
            {
                Grid.SetColumnSpan(label, columns);
                row++;
                column = 0;
            }
            else if (++column >= columns)
            {
                row++;
                column = 0;
            }
        }
        return WrapSection(title, grid);
    }

    private static View WrapSection(string title, View body) => new VerticalStackLayout
    {
        Spacing = 7,
        Children =
        {
            new Label { Text = title, FontSize = 19, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#101828"), Margin = new Thickness(10, 0, 0, 0) },
            new Border { StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 2 }, Content = body }
        }
    };

    private static string ValueText(object? value) => value switch
    {
        null => "--",
        string text when string.IsNullOrWhiteSpace(text) => "--",
        decimal number => number.ToString("0.##"),
        _ => value.ToString() ?? "--"
    };

    private static string BoolText(bool? value) => value switch { true => "是", false => "否", _ => "--" };

    private static string FormatUnit(object? value, string? unit, params string[] aliases)
    {
        var text = ValueText(value).Trim();
        if (text == "--" || string.IsNullOrWhiteSpace(unit)) return text;

        var acceptedUnits = aliases.Append(unit);
        return acceptedUnits.Any(item => text.EndsWith(item, StringComparison.OrdinalIgnoreCase))
            ? text
            : $"{text}{unit}";
    }

    private static string FormatTorsion(string? value)
    {
        var text = ValueText(value).Trim();
        if (text == "--") return text;

        var numberText = text.EndsWith("次", StringComparison.Ordinal) || text.EndsWith("转", StringComparison.Ordinal)
            ? text[..^1].Trim()
            : text;
        return decimal.TryParse(numberText, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
            ? $"{number:0.##}次"
            : FormatUnit(text, "次", "转");
    }

    private static string FormatLowerTolerance(string? value)
    {
        var text = FormatUnit(value, "mm");
        return text == "--" || text.StartsWith('-') ? text : $"-{text}";
    }

    private static string FormatUpperLimit(string? value)
    {
        var text = FormatUnit(value, "mm");
        return text == "--" || text.StartsWith('≤') || text.StartsWith("<=", StringComparison.Ordinal)
            ? text
            : $"≤{text}";
    }
    private static string StateName(string? state) => state?.Trim().ToLowerInvariant() switch
    {
        "raw" => "原料", "pickled" => "酸洗后", "heat_treated" => "热处理后", "drawn" => "拉拔后",
        "finished" => "成品", "bloomed" => "开坯后", _ => "未知"
    };

    private void SetLoading(bool value)
    {
        LoadingIndicator.IsVisible = value;
        LoadingIndicator.IsRunning = value;
    }

    private async void OnBackClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
    private async void OnRescanClicked(object sender, EventArgs e) => await ScanAndQueryAsync();
}
