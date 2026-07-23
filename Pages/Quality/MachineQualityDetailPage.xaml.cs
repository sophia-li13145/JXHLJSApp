using JXHLJSApp.Models.Quality;
using JXHLJSApp.Pages;
using JXHLJSApp.Services;
using JXHLJSApp.Services.Quality;
using Microsoft.Maui.Controls.Shapes;
using System.Globalization;

namespace JXHLJSApp.Pages.Quality;

[QueryProperty(nameof(QualityNo), "qualityNo")]
[QueryProperty(nameof(WorkOrderNo), "workOrderNo")]
[QueryProperty(nameof(InspectStatus), "inspectStatus")]
[QueryProperty(nameof(ManualInspection), "manualInspection")]
[QueryProperty(nameof(ProcessName), "processName")]
[QueryProperty(nameof(WorkOrderStatus), "workOrderStatus")]
[QueryProperty(nameof(QualityType), "qualityType")]
[QueryProperty(nameof(QualityTypeName), "qualityTypeName")]
[QueryProperty(nameof(ListInspectionSchemeName), "inspectionSchemeName")]
public partial class MachineQualityDetailPage : ContentPage
{
    private const string SchemeAcidPickling = "酸洗";
    private const string SchemeBlankOpening = "开坯";
    private const string SchemeBlankOpeningAlias = "开胚";
    private const string SchemeHeatTreatment = "热处理";
    private const string SchemeDrawing = "拉拔";
    private readonly IQualityApi _qualityApi;
    private readonly IScanService _scanService;
    private ProductionQualityDetailDto? _detail;
    private string? _qualityNo;
    private string? _workOrderNo;
    private string? _inspectionSchemeName;
    private string? _inspectStatus;
    private string? _workOrderStatus;
    private string? _processNameFromScan;
    private string? _qualityTypeFromQuery;
    private string? _qualityTypeNameFromQuery;
    private string? _listInspectionSchemeName;
    private string? _qrCode;
    private string? _qualityMaterialId;
    private bool _isManualInspection;
    private bool _manualInspectionFromQuery;
    private bool _hasLoadedDetail;
    private string? _loadedQualityNo;

    public string? QualityNo { get => _qualityNo; set => _qualityNo = Uri.UnescapeDataString(value ?? string.Empty); }
    public string? WorkOrderNo { get => _workOrderNo; set => _workOrderNo = Uri.UnescapeDataString(value ?? string.Empty); }
    public string? InspectStatus { get => _inspectStatus; set => _inspectStatus = Uri.UnescapeDataString(value ?? string.Empty); }
    public string? ManualInspection { get => _manualInspectionFromQuery ? "true" : "false"; set => _manualInspectionFromQuery = string.Equals(Uri.UnescapeDataString(value ?? string.Empty), "true", StringComparison.OrdinalIgnoreCase); }
    public string? ProcessName { get => _processNameFromScan; set => _processNameFromScan = Uri.UnescapeDataString(value ?? string.Empty); }
    public string? WorkOrderStatus { get => _workOrderStatus; set => _workOrderStatus = Uri.UnescapeDataString(value ?? string.Empty); }
    public string? QualityType { get => _qualityTypeFromQuery; set => _qualityTypeFromQuery = Uri.UnescapeDataString(value ?? string.Empty); }
    public string? QualityTypeName { get => _qualityTypeNameFromQuery; set => _qualityTypeNameFromQuery = Uri.UnescapeDataString(value ?? string.Empty); }
    public string? ListInspectionSchemeName { get => _listInspectionSchemeName; set => _listInspectionSchemeName = Uri.UnescapeDataString(value ?? string.Empty); }

    public MachineQualityDetailPage(IQualityApi qualityApi, IScanService scanService)
    {
        InitializeComponent();
        _qualityApi = qualityApi;
        _scanService = scanService;
        CoilDiameterPicker.ItemsSource = new[] { "合格", "不合格" };
        CoilPitchPicker.ItemsSource = new[] { "合格", "不合格" };
        InspectResultPicker.ItemsSource = new[] { "合格", "不合格" };
        CoilDiameterPicker.SelectedIndex = 0;
        CoilPitchPicker.SelectedIndex = 0;
        InspectResultPicker.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_hasLoadedDetail && string.Equals(_loadedQualityNo, _qualityNo, StringComparison.Ordinal))
        {
            return;
        }

        await LoadAsync();
    }

    private async Task RefreshAfterSuccessfulOperationAsync()
    {
        try
        {
            _hasLoadedDetail = false;
            _loadedQualityNo = null;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "刷新失败", ex.Message, "确定");
        }
    }

    private async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(_qualityNo))
        {
            await DisplayAlert("提示", "质检单号为空。", "确定");
            return;
        }

        try
        {
            _isManualInspection = _manualInspectionFromQuery || string.IsNullOrWhiteSpace(_workOrderNo) || IsPatrolInspectionFromList();
            var detail = _isManualInspection
                ? await _qualityApi.GetManualInspectionDetailAsync(_qualityNo)
                : await _qualityApi.GetProductionQualityDetailAsync(_qualityNo, _workOrderNo);
            _detail = detail;
            ApplyScannedProcessNameFallback(detail);
            _inspectionSchemeName = ResolveQualityFlowName(detail);
            if (!string.IsNullOrWhiteSpace(detail.inspectStatus)) _inspectStatus = detail.inspectStatus;
            if (!string.IsNullOrWhiteSpace(detail.workOrderStatus)) _workOrderStatus = detail.workOrderStatus;
            if (string.IsNullOrWhiteSpace(detail.qualityType)) detail.qualityType = _qualityTypeFromQuery;
            if (string.IsNullOrWhiteSpace(detail.qualityTypeName)) detail.qualityTypeName = _qualityTypeNameFromQuery;
            if (!string.IsNullOrWhiteSpace(detail.workOrderNo)) _workOrderNo = detail.workOrderNo;
            var firstMaterial = detail.materialList?.FirstOrDefault();
            _qrCode = FirstNonEmpty(detail.qrCode, firstMaterial?.qrCode);
            _qualityMaterialId = FirstNonEmpty(detail.qualityMaterialId, firstMaterial?.qualityMaterialId);
            ApplySchemeLayout(detail);
            RenderInfo(detail);
            RenderMaterialInfo(detail);
            RenderInspectionItems(detail);
            FillAcidInputs(detail);
            FillHeatTreatmentInputs(detail);
            ActualDiameterEntry.Text = detail.actualDiameterMm;
            StrengthEntry.Text = detail.strengthMpa;
            ElongationEntry.Text = detail.elongationRate;
            SurfaceEntry.Text = detail.surfaceCondition;
            MemoEditor.Text = detail.memo;
            SelectQualifiedOption(CoilDiameterPicker, detail.coilDiameterControl);
            SelectQualifiedOption(CoilPitchPicker, detail.coilPitchControl);
            SelectQualifiedOption(InspectResultPicker, detail.inspectResult);
            ApplyReadOnlyStateIfCompleted();
            _hasLoadedDetail = true;
            _loadedQualityNo = _qualityNo;
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "加载失败", ex.Message, "确定");
        }
    }

    private void ApplySchemeLayout(ProductionQualityDetailDto detail)
    {
        var processName = ResolveProcessName(detail);
        var qualityFlowName = ResolveQualityFlowName(detail);
        var isAcid = IsPicklingScheme(processName);
        var isHeat = IsHeatTreatmentScheme(processName);
        var isDrawing = IsDrawingScheme(processName);
        var isFirstInspection = isDrawing && HasSchemeToken(qualityFlowName, "首检", "首件检");

        TitleLabel.Text = isAcid ? "执行酸洗质检" : isHeat ? "执行热处理质检" : isFirstInspection ? "执行拉拔工序质检" : "执行工序质检";
        InfoTitleLabel.Text = isAcid ? "酸洗任务信息" : isHeat ? "热处理卡片信息" : "生产卡片信息";
        InputTitleLabel.Text = isAcid ? "酸洗检验录入" : isHeat ? "理化检验录入" : "检验项目录入";
        AcidInputPanel.IsVisible = isAcid;
        HeatTreatmentInputPanel.IsVisible = isHeat;
        ProcessInputPanel.IsVisible = !isAcid && !isHeat;
        MemoLabel.IsVisible = !isHeat;
        MemoEditor.IsVisible = !isHeat;
        var isManualPatrol = ShouldUseManualInspectionResultApi();
        var isSubmitOnlyProcess = isAcid || (isDrawing && !isManualPatrol);
        SubmitButton.Text = "提交质检";
        CompleteButton.IsVisible = !isSubmitOnlyProcess;
        Grid.SetColumnSpan(SubmitButton, isSubmitOnlyProcess ? 2 : 1);
        ScanMaterialButton.IsVisible = false;
        InfoScanMaterialButton.IsVisible = !IsInspectionCompleted(_inspectStatus) && !isAcid && (!isDrawing || isManualPatrol) && (IsSamplingOrFullScheme(processName) || IsProcessCardScheme(processName));
    }

    private void FillAcidInputs(ProductionQualityDetailDto detail)
    {
        AcidDatePicker.Date = ResolveAcidInspectDate(detail);
        PhosphatingTemperatureEntry.Text = detail.phosphatingTemperature;
        TotalAcidEntry.Text = detail.totalAcid;
        FreeAcidEntry.Text = detail.freeAcid;
        SaponificationTemperatureEntry.Text = detail.saponificationTemperature;
        SaponificationPhEntry.Text = detail.saponificationPhValue;
        HydrochloricAcid1Entry.Text = detail.hydrochloricAcidConcentration1;
        HydrochloricAcid2Entry.Text = detail.hydrochloricAcidConcentration2;
        TotalAcidSamplingEntry.Text = detail.totalAcidSampling;
        FreeAcidSamplingEntry.Text = detail.freeAcidSampling;
        AcidRatioEntry.Text = detail.acidRatio;
        RecorderEntry.Text = BuildCurrentRecorderDisplay();
    }


    private static DateTime ResolveAcidInspectDate(ProductionQualityDetailDto detail)
    {
        return DateTime.TryParse(FirstNonEmpty(detail.inspectDate), CultureInfo.CurrentCulture, DateTimeStyles.None, out var inspectDate)
            ? inspectDate
            : DateTime.Today;
    }

    private void FillHeatTreatmentInputs(ProductionQualityDetailDto detail)
    {
        HeatActualDiameterEntry.Text = detail.actualDiameterMm;
        StandardDiameterEntry.Text = FirstNonEmpty(detail.standardDiameterMm, detail.productDiameter);
        BrokenDiameterEntry.Text = detail.brokenDiameterMm;
        SectionShrinkageLabel.Text = CalculateSectionShrinkageText();
        TensileStrengthEntry.Text = FirstNonEmpty(detail.tensileStrengthMpa, detail.strengthMpa);
        HeatElongationEntry.Text = detail.elongationRate;
        TwistCountEntry.Text = detail.twistCount;
    }

    private void RenderInfo(ProductionQualityDetailDto detail)
    {
        InfoGrid.Children.Clear();
        InfoGrid.RowDefinitions.Clear();
        var rows = BuildInfoRows(detail);

        for (var i = 0; i < rows.Length; i++)
        {
            if (i % 2 == 0) InfoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var cell = CreateInfoCell(rows[i].Label, rows[i].Value);
            InfoGrid.Add(cell, i % 2, i / 2);
            if (rows.Length == 1)
            {
                Grid.SetColumnSpan(cell, 2);
            }
        }
    }

    private void ApplyScannedProcessNameFallback(ProductionQualityDetailDto detail)
    {
        if (_isManualInspection && string.IsNullOrWhiteSpace(detail.processName) && !string.IsNullOrWhiteSpace(_processNameFromScan))
        {
            detail.processName = _processNameFromScan;
        }
    }

    private static string? ResolveProcessName(ProductionQualityDetailDto detail)
    {
        return FirstNonEmpty(detail.processName, detail.processCode).Trim();
    }

    private static string? ResolveQualityFlowName(ProductionQualityDetailDto detail)
    {
        return FirstNonEmpty(detail.inspectionSchemeName, detail.qualityTypeName, detail.inspectionSchemeTypeName, detail.processName, detail.processCode).Trim();
    }

    private static (string Label, string? Value)[] BuildInfoRows(ProductionQualityDetailDto detail)
    {
        var schemeName = ResolveProcessName(detail);
        if (IsPicklingScheme(schemeName))
        {
            return new[]
            {
                ("检验日期", DateTime.Now.ToString("yyyy-MM-dd"))
            };
        }

        if (IsHeatTreatmentScheme(schemeName))
        {
            return new[]
            {
                ("日期", ResolveHeatTreatmentCardDate(detail)), ("机台", detail.deviceName ?? detail.deviceCode),
                ("批号", FirstNonEmpty(detail.batchNo, detail.businessType)), ("炉号", detail.furnaceNo),
                ("产地", FirstNonEmpty(detail.originPlace, detail.freeAcid)), ("钢号", detail.steelGrade),
                ("工号", detail.workOrderNo), ("班次", FirstNonEmpty(detail.shiftNo, detail.targetSpecification)),
                ("盘号", FirstNonEmpty(detail.plateNo, detail.inputSpecification))
            };
        }

        if (IsProcessCardScheme(schemeName))
        {
            return new[]
            {
                ("日期", ResolveProcessCardDate(detail)), ("机台", ResolveMachine(detail)),
                ("客户代码", FirstNonEmpty(detail.customerCode, detail.businessType)), ("炉号", detail.furnaceNo),
                ("钢号", detail.steelGrade), ("挂牌", detail.inputSpecification),
                ("工号", detail.workOrderNo), ("件号", ResolvePieceNo(detail)),
                ("产地", FirstNonEmpty(detail.originPlace, detail.freeAcid)), ("投料直径mm", detail.inputDiameterMm),
                ("成品直径mm", ResolveProductDiameter(detail)), ("上公差", FormatSignedTolerance(detail.upperToleranceValue, '+')),
                ("下公差", FormatSignedTolerance(detail.lowerToleranceValue, '-')), ("强度要求", detail.spoolWeightRequirement),
                ("圈径", FirstNonEmpty(detail.workOrderRingDiameter, detail.coilDiameterControl)), ("圈径控制", FirstNonEmpty(detail.workOrderCoilDiameterControl, detail.coilDiameterControl)),
                ("圈距控制", FormatCoilPitchControl(FirstNonEmpty(detail.workOrderCoilPitchControl, detail.coilPitchControl)))
            };
        }

        return new[]
        {
            ("日期", DateTime.Now.ToString("yyyyMMdd")), ("巡检单号", detail.qualityNo),
            ("工单号", detail.workOrderNo), ("工序", FirstNonEmpty(detail.processName, detail.processCode)),
            ("质检方案", detail.inspectionSchemeName), ("方案类型", detail.inspectionSchemeTypeName),
            ("质检类型", FirstNonEmpty(detail.qualityTypeName, detail.qualityType)), ("状态", detail.inspectStatus),
            ("工单状态", detail.workOrderStatus),
            ("设备", FirstNonEmpty(detail.deviceName, detail.deviceCode)), ("炉号", detail.furnaceNo),
            ("钢号", detail.steelGrade), ("件号", ResolvePieceNo(detail)),
            ("规格", FirstNonEmpty(detail.targetSpecification, detail.inputSpecification))
        };
    }

    private void RenderMaterialInfo(ProductionQualityDetailDto detail)
    {
        var material = detail.materialList?.FirstOrDefault();
        MaterialInfoCard.IsVisible = _isManualInspection && material is not null;
        MaterialGrid.Children.Clear();
        MaterialGrid.RowDefinitions.Clear();
        if (material is null) return;

        var rows = new[]
        {
            ("物料编码", material.materialCode), ("物料名称", material.materialName),
            ("二维码", material.qrCode), ("扫码次数", material.qrTimes?.ToString()),
            ("质检物料ID", material.qualityMaterialId), ("工单号", material.workOrderNo),
            ("炉号", material.furnaceNo), ("钢号", material.steelGrade),
            ("件号", FirstNonEmpty(material.pieceNo, "1")), ("规格", FirstNonEmpty(material.spec, material.inputSpecification, material.targetSpecification)),
            ("设备", FirstNonEmpty(material.deviceName, material.deviceCode)),
            ("实测直径", material.actualDiameterMm), ("成品直径", material.productDiameter),
            ("强度", material.strengthMpa), ("表面状态", material.surfaceCondition),
            ("结果已保存", material.resultSaved ? "是" : "否"), ("备注", material.memo)
        };

        for (var i = 0; i < rows.Length; i++)
        {
            if (i % 2 == 0) MaterialGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            MaterialGrid.Add(CreateInfoCell(rows[i].Item1, rows[i].Item2), i % 2, i / 2);
        }
    }

    private void RenderInspectionItems(ProductionQualityDetailDto detail)
    {
        InspectionItemList.Children.Clear();
        var items = detail.inspectionItemList ?? new List<ProductionQualityInspectionItemDto>();
        InspectionItemCard.IsVisible = _isManualInspection && items.Count > 0;
        foreach (var item in items)
        {
            var standard = FirstNonEmpty(item.inspectionStandard, item.standardValue, BuildLimitText(item));
            InspectionItemList.Children.Add(new Border
            {
                BackgroundColor = Color.FromArgb("#F8FAFD"),
                Stroke = Color.FromArgb("#DDE6F1"),
                StrokeThickness = 1,
                Padding = 12,
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Content = new VerticalStackLayout
                {
                    Spacing = 4,
                    Children =
                    {
                        new Label { Text = FirstNonEmpty(item.itemName, item.itemCode, "检验项目"), TextColor = Color.FromArgb("#051B3D"), FontAttributes = FontAttributes.Bold },
                        new Label { Text = $"标准：{(string.IsNullOrWhiteSpace(standard) ? "-" : standard)}", TextColor = Color.FromArgb("#38557C"), FontSize = 13 },
                        new Label { Text = $"方式：{FirstNonEmpty(item.inspectionMode, "-")}  单位：{FirstNonEmpty(item.unit, "-")}", TextColor = Color.FromArgb("#38557C"), FontSize = 13 }
                    }
                }
            });
        }
    }


    private static string ResolveMachine(ProductionQualityDetailDto detail)
    {
        return FirstNonEmpty(detail.machine, detail.deviceName, detail.deviceCode);
    }

    private static string ResolveProductDiameter(ProductionQualityDetailDto detail)
    {
        return FirstNonEmpty(detail.productDiameter, detail.prodcutDiameter);
    }

    private static string ResolveProcessCardDate(ProductionQualityDetailDto detail)
    {
        if (DateTime.TryParse(FirstNonEmpty(detail.productionDate, detail.inspectDate), CultureInfo.CurrentCulture, DateTimeStyles.None, out var date))
        {
            return date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        return FirstNonEmpty(detail.productionDate, DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
    }

    private static string ResolvePieceNo(ProductionQualityDetailDto detail)
    {
        return FirstNonEmpty(detail.pieceNo, detail.materialList?.FirstOrDefault()?.pieceNo, "1");
    }

    private static string ResolveHeatTreatmentCardDate(ProductionQualityDetailDto detail)
    {
        return DateTime.TryParse(FirstNonEmpty(detail.inspectDate), CultureInfo.CurrentCulture, DateTimeStyles.None, out var inspectDate)
            ? inspectDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
            : DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
    }

    private static string BuildLimitText(ProductionQualityInspectionItemDto item)
    {
        var lower = string.IsNullOrWhiteSpace(item.lowerLimit) ? "-" : item.lowerLimit;
        var upper = string.IsNullOrWhiteSpace(item.upperLimit) ? "-" : item.upperLimit;
        return lower == "-" && upper == "-" ? string.Empty : $"{lower} ~ {upper}";
    }

    private static string FormatCoilPitchControl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var text = value.Trim();
        text = text.StartsWith("<=", StringComparison.Ordinal) ? text[2..].TrimStart() : text;
        text = text.StartsWith("≤", StringComparison.Ordinal) ? text[1..].TrimStart() : text;
        text = text.EndsWith("mm", StringComparison.OrdinalIgnoreCase) ? text[..^2].TrimEnd() : text;
        return string.IsNullOrWhiteSpace(text) ? string.Empty : $"≤{text}mm";
    }

    private static string FormatSignedTolerance(string? value, char sign)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var text = value.Trim();
        text = text.TrimStart('+', '-', '＋', '－').TrimStart();
        return string.IsNullOrWhiteSpace(text) ? string.Empty : $"{sign}{text}";
    }

    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;

    private static string BuildCurrentRecorderDisplay()
    {
        var realName = Preferences.Get(UserSessionKeys.RealName, string.Empty).Trim();
        var workNumber = Preferences.Get(UserSessionKeys.WorkNumber, string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(realName))
        {
            return string.IsNullOrWhiteSpace(workNumber) ? string.Empty : workNumber;
        }

        return string.IsNullOrWhiteSpace(workNumber) ? realName : $"{realName} ({workNumber})";
    }

    private static string BuildCurrentRecorderUsername(string? recorderDisplay)
    {
        return FirstNonEmpty(
            Preferences.Get(UserSessionKeys.UserName, string.Empty).Trim(),
            Preferences.Get(UserSessionKeys.RealName, string.Empty).Trim(),
            recorderDisplay?.Trim(),
            Preferences.Get(UserSessionKeys.WorkNumber, string.Empty).Trim(),
            ExtractWorkNumberFromRecorderDisplay(recorderDisplay));
    }

    private static string ExtractWorkNumberFromRecorderDisplay(string? recorderDisplay)
    {
        if (string.IsNullOrWhiteSpace(recorderDisplay)) return string.Empty;

        var start = recorderDisplay.LastIndexOf('(');
        var end = recorderDisplay.LastIndexOf(')');
        return start >= 0 && end > start
            ? recorderDisplay[(start + 1)..end].Trim()
            : string.Empty;
    }

    private void ApplyReadOnlyStateIfCompleted()
    {
        if (!IsInspectionCompleted(_inspectStatus)) return;

        SetReadOnly(Content);
        ActionBar.IsVisible = false;
    }

    private static bool IsInspectionCompleted(string? inspectStatus)
    {
        return IsCompletedStatus(inspectStatus);
    }


    private static bool IsCompletedStatus(string? status)
    {
        return string.Equals(status, "3", StringComparison.Ordinal) ||
            string.Equals(status, "检验完成", StringComparison.Ordinal) ||
            string.Equals(status, "已完成", StringComparison.Ordinal) ||
            string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "complete", StringComparison.OrdinalIgnoreCase);
    }

    private static void SetReadOnly(Element element)
    {
        switch (element)
        {
            case Entry entry:
                entry.IsReadOnly = true;
                break;
            case Editor editor:
                editor.IsReadOnly = true;
                break;
            case Picker picker:
                picker.IsEnabled = false;
                break;
            case DatePicker datePicker:
                datePicker.IsEnabled = false;
                break;
            case Button button:
                button.IsEnabled = false;
                break;
        }

        switch (element)
        {
            case Layout layout:
                foreach (var child in layout.Children.OfType<Element>()) SetReadOnly(child);
                break;
            case Border border when border.Content is Element borderContent:
                SetReadOnly(borderContent);
                break;
            case ScrollView scrollView when scrollView.Content is Element scrollContent:
                SetReadOnly(scrollContent);
                break;
            case ContentView contentView when contentView.Content is Element viewContent:
                SetReadOnly(viewContent);
                break;
        }
    }

    private static View CreateInfoCell(string label, string? value)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = GridLength.Star }),
            ColumnSpacing = 10,
            HorizontalOptions = LayoutOptions.Fill
        };
        var labelColor = Color.FromArgb("#38557C");
        var valueColor = Color.FromArgb("#0042AD");
        grid.Add(new Label { Text = label, TextColor = labelColor, FontSize = 14, LineBreakMode = LineBreakMode.NoWrap });
        grid.Add(new Label
        {
            Text = string.IsNullOrWhiteSpace(value) ? "-" : value,
            TextColor = valueColor,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Fill,
            HorizontalTextAlignment = TextAlignment.End,
            LineBreakMode = LineBreakMode.NoWrap
        }, 1);
        return grid;
    }

    private static void SelectQualifiedOption(Picker picker, string? value)
    {
        picker.SelectedItem = string.Equals(value, "不合格", StringComparison.Ordinal) ? "不合格" : "合格";
    }

    private static bool IsPicklingScheme(string? schemeName)
    {
        return HasSchemeToken(schemeName, SchemeAcidPickling);
    }

    private static bool IsHeatTreatmentScheme(string? schemeName)
    {
        return HasSchemeToken(schemeName, SchemeHeatTreatment);
    }

    private static bool IsSamplingOrFullScheme(string? schemeName)
    {
        return IsBlankOpeningScheme(schemeName) || HasSchemeToken(schemeName, "抽检") || IsHeatTreatmentScheme(schemeName);
    }

    private static bool IsBlankOpeningScheme(string? schemeName)
    {
        return HasSchemeToken(schemeName, SchemeBlankOpening, SchemeBlankOpeningAlias);
    }

    private static bool IsProcessCardScheme(string? schemeName)
    {
        return HasSchemeToken(schemeName, SchemeBlankOpening, SchemeBlankOpeningAlias, SchemeDrawing, "抽检") || IsFirstInspectionScheme(schemeName);
    }

    private static bool IsDrawingScheme(string? schemeName)
    {
        return HasSchemeToken(schemeName, SchemeDrawing);
    }

    private static bool IsFirstInspectionScheme(string? schemeName)
    {
        return HasSchemeToken(schemeName, SchemeDrawing) && HasSchemeToken(schemeName, "首检", "首件检");
    }

    private string? CurrentProcessName => _detail is null ? _inspectionSchemeName : ResolveProcessName(_detail);

    private bool ShouldUsePicklingCommit()
    {
        return IsPicklingScheme(CurrentProcessName) || IsPicklingScheme(_inspectionSchemeName);
    }

    private bool ShouldUseFirstInspectionCommit()
    {
        return IsDrawingScheme(CurrentProcessName) || IsFirstInspectionScheme(_inspectionSchemeName);
    }

    private bool ShouldUseSamplingOrFullCommit()
    {
        return IsHeatTreatmentScheme(CurrentProcessName) ||
            IsBlankOpeningScheme(CurrentProcessName) ||
            IsSamplingOrFullScheme(_inspectionSchemeName);
    }

    private static bool HasSchemeToken(string? schemeName, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(schemeName)) return false;
        return tokens.Any(token => schemeName.Contains(token, StringComparison.Ordinal));
    }


    private void OnIncrementNumericClicked(object sender, EventArgs e)
    {
        AdjustNumericEntry(sender, 1m);
    }

    private void OnDecrementNumericClicked(object sender, EventArgs e)
    {
        AdjustNumericEntry(sender, -1m);
    }

    private static void AdjustNumericEntry(object sender, decimal delta)
    {
        if (sender is not Button { CommandParameter: Entry entry } || entry.IsReadOnly) return;

        var text = entry.Text?.Trim();
        var currentValue = decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsedValue)
            ? parsedValue
            : 0m;
        entry.Text = (currentValue + delta).ToString("0.##", CultureInfo.CurrentCulture);
    }

    private void OnHeatDiameterTextChanged(object sender, TextChangedEventArgs e)
    {
        SectionShrinkageLabel.Text = CalculateSectionShrinkageText();
    }

    private string CalculateSectionShrinkageText()
    {
        if (!TryParseDecimal(HeatActualDiameterEntry.Text, out var actualDiameter) ||
            !TryParseDecimal(BrokenDiameterEntry.Text, out var brokenDiameter) ||
            actualDiameter <= 0 || brokenDiameter < 0 || brokenDiameter > actualDiameter)
        {
            return "-";
        }

        var rate = (1 - (brokenDiameter * brokenDiameter) / (actualDiameter * actualDiameter)) * 100;
        return $"{rate:0.##}%";
    }

    private static bool TryParseDecimal(string? text, out decimal value)
    {
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value) ||
            decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }



    private bool ShouldUseManualInspectionAddMaterial()
    {
        return ShouldUseManualInspectionResultApi();
    }

    private bool ShouldUseManualInspectionResultApi()
    {
        return _isManualInspection || IsPatrolInspectionFromList();
    }

    private bool IsPatrolInspectionFromList()
    {
        return HasSchemeToken(_listInspectionSchemeName, "巡检");
    }

    private bool ShouldUseSamplingOrFullComplete()
    {
        if (ShouldUseManualInspectionResultApi()) return false;

        var processName = _detail is null ? _inspectionSchemeName : ResolveProcessName(_detail);
        return IsHeatTreatmentScheme(processName) ||
            IsBlankOpeningScheme(processName) ||
            IsHeatTreatmentScheme(_inspectionSchemeName) ||
            IsBlankOpeningScheme(_inspectionSchemeName);
    }

    private async void OnScanMaterialClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_qualityNo) || (!_isManualInspection && string.IsNullOrWhiteSpace(_workOrderNo)))
        {
            await DisplayAlert("提示", _isManualInspection ? "质检单号为空，无法扫码物料。" : "质检单号或工单号为空，无法扫码物料。", "确定");
            return;
        }

        var code = await _scanService.ScanAsync("生产质检扫码物料");
        if (string.IsNullOrWhiteSpace(code)) return;

        try
        {
            var request = new ProductionQualityScanMaterialRequestDto
            {
                qrCode = code,
                qualityNo = _qualityNo,
                workOrderNo = _workOrderNo
            };
            var manualInputState = ShouldPreserveManualInputsOnMaterialScan() ? CaptureManualInputState() : null;
            var material = await ScanMaterialForCurrentFlowAsync(request);
            if (ShouldRefreshDetailAfterMaterialScan())
            {
                await RefreshDetailAfterMaterialScanAsync();
            }
            ApplyScannedMaterial(material, code);
            RestoreManualInputState(manualInputState);
            await DisplayAlert("扫码成功", "物料信息已更新到当前质检页面。", "确定");
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "扫码物料失败", ex.Message, "确定");
        }
    }

    private Task<ProductionQualityScanMaterialDto> ScanMaterialForCurrentFlowAsync(ProductionQualityScanMaterialRequestDto request)
    {
        return ShouldUseManualInspectionAddMaterial()
            ? _qualityApi.AddManualInspectionMaterialAsync(request)
            : _qualityApi.ScanProductionQualityMaterialAsync(request);
    }

    private bool ShouldRefreshDetailAfterMaterialScan()
    {
        return ShouldUseManualInspectionResultApi() ||
            IsHeatTreatmentScheme(CurrentProcessName) ||
            IsBlankOpeningScheme(CurrentProcessName) ||
            IsHeatTreatmentScheme(_inspectionSchemeName) ||
            IsBlankOpeningScheme(_inspectionSchemeName);
    }

    private async Task RefreshDetailAfterMaterialScanAsync()
    {
        _hasLoadedDetail = false;
        _loadedQualityNo = null;
        await LoadAsync();
    }

    private bool ShouldPreserveManualInputsOnMaterialScan()
    {
        return ShouldUseManualInspectionResultApi() ||
            IsHeatTreatmentScheme(CurrentProcessName) ||
            IsBlankOpeningScheme(CurrentProcessName) ||
            IsHeatTreatmentScheme(_inspectionSchemeName) ||
            IsBlankOpeningScheme(_inspectionSchemeName);
    }

    private ManualInputState CaptureManualInputState()
    {
        return new ManualInputState(
            ActualDiameterEntry.Text,
            StrengthEntry.Text,
            ElongationEntry.Text,
            SurfaceEntry.Text,
            HeatActualDiameterEntry.Text,
            BrokenDiameterEntry.Text,
            TensileStrengthEntry.Text,
            HeatElongationEntry.Text,
            TwistCountEntry.Text,
            MemoEditor.Text,
            CoilDiameterPicker.SelectedIndex,
            CoilPitchPicker.SelectedIndex,
            InspectResultPicker.SelectedIndex);
    }

    private void RestoreManualInputState(ManualInputState? state)
    {
        if (state is null) return;

        RestoreText(ActualDiameterEntry, state.ActualDiameter);
        RestoreText(StrengthEntry, state.Strength);
        RestoreText(ElongationEntry, state.Elongation);
        RestoreText(SurfaceEntry, state.Surface);
        RestoreText(HeatActualDiameterEntry, state.HeatActualDiameter);
        RestoreText(BrokenDiameterEntry, state.BrokenDiameter);
        RestoreText(TensileStrengthEntry, state.TensileStrength);
        RestoreText(HeatElongationEntry, state.HeatElongation);
        RestoreText(TwistCountEntry, state.TwistCount);
        RestoreText(MemoEditor, state.Memo);
        RestorePicker(CoilDiameterPicker, state.CoilDiameterIndex);
        RestorePicker(CoilPitchPicker, state.CoilPitchIndex);
        RestorePicker(InspectResultPicker, state.InspectResultIndex);
    }

    private static void RestoreText(InputView input, string? text)
    {
        if (!string.IsNullOrWhiteSpace(text)) input.Text = text;
    }

    private static void RestorePicker(Picker picker, int selectedIndex)
    {
        if (selectedIndex >= 0 && selectedIndex < picker.Items.Count) picker.SelectedIndex = selectedIndex;
    }

    private sealed record ManualInputState(
        string? ActualDiameter,
        string? Strength,
        string? Elongation,
        string? Surface,
        string? HeatActualDiameter,
        string? BrokenDiameter,
        string? TensileStrength,
        string? HeatElongation,
        string? TwistCount,
        string? Memo,
        int CoilDiameterIndex,
        int CoilPitchIndex,
        int InspectResultIndex);

    private void ApplyScannedMaterial(ProductionQualityScanMaterialDto material, string fallbackQrCode)
    {
        _qrCode = FirstNonEmpty(material.qrCode, fallbackQrCode);
        _qualityMaterialId = FirstNonEmpty(material.qualityMaterialId, _qualityMaterialId);
        if (_detail is null) _detail = new ProductionQualityDetailDto { workOrderNo = _workOrderNo };

        _detail.acidRatio = FirstNonEmpty(material.acidRatio, _detail.acidRatio);
        _detail.actualDiameterMm = FirstNonEmpty(material.actualDiameterMm, _detail.actualDiameterMm);
        _detail.batchNo = FirstNonEmpty(material.batchNo, _detail.batchNo);
        _detail.businessType = FirstNonEmpty(material.businessType, _detail.businessType);
        _detail.coilDiameterControl = FirstNonEmpty(material.coilDiameterControl, _detail.coilDiameterControl);
        _detail.coilPitchControl = FirstNonEmpty(material.coilPitchControl, _detail.coilPitchControl);
        _detail.customerCode = FirstNonEmpty(material.customerCode, _detail.customerCode);
        _detail.deviceCode = FirstNonEmpty(material.deviceCode, _detail.deviceCode);
        _detail.deviceName = FirstNonEmpty(material.deviceName, material.machineNo, material.machine, _detail.deviceName);
        _detail.machine = FirstNonEmpty(material.machine, material.machineNo, material.deviceName, _detail.machine);
        _detail.elongationRate = FirstNonEmpty(material.elongationRate, _detail.elongationRate);
        _detail.freeAcid = FirstNonEmpty(material.freeAcid, _detail.freeAcid);
        _detail.freeAcidSampling = FirstNonEmpty(material.freeAcidSampling, _detail.freeAcidSampling);
        _detail.furnaceNo = FirstNonEmpty(material.furnaceNo, _detail.furnaceNo);
        _detail.hydrochloricAcidConcentration1 = FirstNonEmpty(material.hydrochloricAcidConcentration1, _detail.hydrochloricAcidConcentration1);
        _detail.hydrochloricAcidConcentration2 = FirstNonEmpty(material.hydrochloricAcidConcentration2, _detail.hydrochloricAcidConcentration2);
        _detail.inputDiameterMm = FirstNonEmpty(material.inputDiameterMm, _detail.inputDiameterMm);
        _detail.inputSpecification = FirstNonEmpty(material.inputSpecification, material.listing, _detail.inputSpecification);
        _detail.inspectResult = FirstNonEmpty(material.inspectResult, _detail.inspectResult);
        _detail.inspectionSchemeCode = FirstNonEmpty(material.inspectionSchemeCode, _detail.inspectionSchemeCode);
        _detail.inspectionSchemeName = FirstNonEmpty(material.inspectionSchemeName, _detail.inspectionSchemeName);
        _detail.lowerToleranceValue = FirstNonEmpty(material.lowerToleranceValue, _detail.lowerToleranceValue);
        _detail.memo = FirstNonEmpty(material.memo, _detail.memo);
        _detail.phosphatingTemperature = FirstNonEmpty(material.phosphatingTemperature, _detail.phosphatingTemperature);
        _detail.pieceNo = FirstNonEmpty(material.pieceNo, _detail.pieceNo);
        _detail.originPlace = FirstNonEmpty(material.originPlace, _detail.originPlace);
        _detail.plateNo = FirstNonEmpty(material.plateNo, _detail.plateNo);
        _detail.productDiameter = FirstNonEmpty(material.productDiameter, material.prodcutDiameter, _detail.productDiameter);
        _detail.prodcutDiameter = FirstNonEmpty(material.prodcutDiameter, material.productDiameter, _detail.prodcutDiameter);
        _detail.productionDate = FirstNonEmpty(material.productionDate, _detail.productionDate);
        _detail.qrCode = _qrCode;
        _detail.qualityMaterialId = _qualityMaterialId;
        _detail.saponificationPhValue = FirstNonEmpty(material.saponificationPhValue, _detail.saponificationPhValue);
        _detail.saponificationTemperature = FirstNonEmpty(material.saponificationTemperature, _detail.saponificationTemperature);
        _detail.shiftNo = FirstNonEmpty(material.shiftNo, _detail.shiftNo);
        _detail.spoolWeightRequirement = FirstNonEmpty(material.spoolWeightRequirement, _detail.spoolWeightRequirement);
        _detail.steelGrade = FirstNonEmpty(material.steelGrade, _detail.steelGrade);
        _detail.strengthMpa = FirstNonEmpty(material.strengthMpa, _detail.strengthMpa);
        _detail.surfaceCondition = FirstNonEmpty(material.surfaceCondition, _detail.surfaceCondition);
        _detail.targetSpecification = FirstNonEmpty(material.targetSpecification, _detail.targetSpecification);
        _detail.totalAcid = FirstNonEmpty(material.totalAcid, _detail.totalAcid);
        _detail.totalAcidSampling = FirstNonEmpty(material.totalAcidSampling, _detail.totalAcidSampling);
        _detail.upperToleranceValue = FirstNonEmpty(material.upperToleranceValue, _detail.upperToleranceValue);
        _detail.workOrderRingDiameter = FirstNonEmpty(material.workOrderRingDiameter, _detail.workOrderRingDiameter);
        _detail.workOrderCoilDiameterControl = FirstNonEmpty(material.workOrderCoilDiameterControl, _detail.workOrderCoilDiameterControl);
        _detail.workOrderCoilPitchControl = FirstNonEmpty(material.workOrderCoilPitchControl, _detail.workOrderCoilPitchControl);
        _detail.workOrderNo = FirstNonEmpty(material.workOrderNo, _detail.workOrderNo);
        if (string.IsNullOrWhiteSpace(_detail.batchNo)) _detail.batchNo = material.productionDate;
        _workOrderNo = FirstNonEmpty(_detail.workOrderNo, _workOrderNo);
        _inspectionSchemeName = ResolveQualityFlowName(_detail);

        if (!string.IsNullOrWhiteSpace(material.materialCode) || !string.IsNullOrWhiteSpace(material.materialName) || !string.IsNullOrWhiteSpace(material.qrCode))
        {
            _detail.materialList = new List<ProductionQualityMaterialDto> { CreateScannedMaterialInfo(material, fallbackQrCode) };
        }

        RenderInfo(_detail);
        RenderMaterialInfo(_detail);
        RenderInspectionItems(_detail);
        FillHeatTreatmentInputs(_detail);
        ActualDiameterEntry.Text = _detail.actualDiameterMm;
        StrengthEntry.Text = _detail.strengthMpa;
        ElongationEntry.Text = _detail.elongationRate;
        SurfaceEntry.Text = _detail.surfaceCondition;
        MemoEditor.Text = _detail.memo;
        SelectQualifiedOption(CoilDiameterPicker, _detail.coilDiameterControl);
        SelectQualifiedOption(CoilPitchPicker, _detail.coilPitchControl);
        SelectQualifiedOption(InspectResultPicker, _detail.inspectResult);
    }



    private static ProductionQualityMaterialDto CreateScannedMaterialInfo(ProductionQualityScanMaterialDto material, string fallbackQrCode)
    {
        return new ProductionQualityMaterialDto
        {
            acidRatio = material.acidRatio,
            actualDiameterMm = material.actualDiameterMm,
            businessType = material.businessType,
            coilDiameterControl = material.coilDiameterControl,
            coilPitchControl = material.coilPitchControl,
            deviceCode = material.deviceCode,
            deviceName = FirstNonEmpty(material.deviceName, material.machineNo, material.machine),
            machine = FirstNonEmpty(material.machine, material.machineNo, material.deviceName),
            elongationRate = material.elongationRate,
            freeAcid = material.freeAcid,
            freeAcidSampling = material.freeAcidSampling,
            furnaceNo = material.furnaceNo,
            hydrochloricAcidConcentration1 = material.hydrochloricAcidConcentration1,
            hydrochloricAcidConcentration2 = material.hydrochloricAcidConcentration2,
            inputDiameterMm = material.inputDiameterMm,
            inputSpecification = FirstNonEmpty(material.inputSpecification, material.listing),
            inspectResult = material.inspectResult,
            inspectionSchemeCode = material.inspectionSchemeCode,
            inspectionSchemeName = material.inspectionSchemeName,
            lowerToleranceValue = material.lowerToleranceValue,
            materialCode = material.materialCode,
            materialName = material.materialName,
            memo = material.memo,
            phosphatingTemperature = material.phosphatingTemperature,
            pieceNo = material.pieceNo,
            productDiameter = FirstNonEmpty(material.productDiameter, material.prodcutDiameter),
            prodcutDiameter = FirstNonEmpty(material.prodcutDiameter, material.productDiameter),
            productionDate = material.productionDate,
            qrCode = FirstNonEmpty(material.qrCode, fallbackQrCode),
            qrTimes = material.qrTimes,
            qualityMaterialId = material.qualityMaterialId,
            resultSaved = material.resultSaved,
            saponificationPhValue = material.saponificationPhValue,
            saponificationTemperature = material.saponificationTemperature,
            spec = material.spec,
            spoolWeightRequirement = material.spoolWeightRequirement,
            steelGrade = material.steelGrade,
            strengthMpa = material.strengthMpa,
            surfaceCondition = material.surfaceCondition,
            targetSpecification = material.targetSpecification,
            totalAcid = material.totalAcid,
            totalAcidSampling = material.totalAcidSampling,
            upperToleranceValue = material.upperToleranceValue,
            workOrderRingDiameter = material.workOrderRingDiameter,
            workOrderCoilDiameterControl = material.workOrderCoilDiameterControl,
            workOrderCoilPitchControl = material.workOrderCoilPitchControl,
            workOrderNo = material.workOrderNo
        };
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnSubmitClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_qualityNo) || string.IsNullOrWhiteSpace(_workOrderNo))
        {
            await DisplayAlert("提示", "质检单号或工单号为空，无法提交。", "确定");
            return;
        }

        try
        {
            var request = new ProductionQualityCommitRequestDto
            {
                acidRatio = AcidRatioEntry.Text?.Trim(),
                actualDiameterMm = HeatTreatmentInputPanel.IsVisible ? HeatActualDiameterEntry.Text?.Trim() : ActualDiameterEntry.Text?.Trim(),
                brokenDiameterMm = BrokenDiameterEntry.Text?.Trim(),
                coilDiameterControl = CoilDiameterPicker.SelectedItem?.ToString(),
                coilPitchControl = CoilPitchPicker.SelectedItem?.ToString(),
                elongationRate = HeatTreatmentInputPanel.IsVisible ? HeatElongationEntry.Text?.Trim() : ElongationEntry.Text?.Trim(),
                inspectResult = InspectResultPicker.SelectedItem?.ToString(),
                sectionShrinkageRate = SectionShrinkageLabel.Text == "-" ? null : SectionShrinkageLabel.Text,
                freeAcid = FreeAcidEntry.Text?.Trim(),
                freeAcidSampling = FreeAcidSamplingEntry.Text?.Trim(),
                hydrochloricAcidConcentration1 = HydrochloricAcid1Entry.Text?.Trim(),
                hydrochloricAcidConcentration2 = HydrochloricAcid2Entry.Text?.Trim(),
                memo = MemoEditor.Text?.Trim(),
                phosphatingTemperature = PhosphatingTemperatureEntry.Text?.Trim(),
                qualityNo = _qualityNo,
                recorder = RecorderEntry.Text?.Trim(),
                saponificationPhValue = SaponificationPhEntry.Text?.Trim(),
                saponificationTemperature = SaponificationTemperatureEntry.Text?.Trim(),
                standardDiameterMm = StandardDiameterEntry.Text?.Trim(),
                strengthMpa = HeatTreatmentInputPanel.IsVisible ? TensileStrengthEntry.Text?.Trim() : StrengthEntry.Text?.Trim(),
                tensileStrengthMpa = TensileStrengthEntry.Text?.Trim(),
                surfaceCondition = SurfaceEntry.Text?.Trim(),
                totalAcid = TotalAcidEntry.Text?.Trim(),
                twistCount = TwistCountEntry.Text?.Trim(),
                totalAcidSampling = TotalAcidSamplingEntry.Text?.Trim(),
                workOrderNo = _workOrderNo
            };

            var useManualInspectionApi = ShouldUseManualInspectionResultApi();
            var isAcid = !useManualInspectionApi && ShouldUsePicklingCommit();
            var shouldStayAfterSubmit = IsHeatTreatmentScheme(CurrentProcessName) ||
                IsHeatTreatmentScheme(_inspectionSchemeName) ||
                IsBlankOpeningScheme(CurrentProcessName) ||
                IsBlankOpeningScheme(_inspectionSchemeName);
            var useFirstInspectionCommit = !useManualInspectionApi && ShouldUseFirstInspectionCommit();
            var useSamplingOrFullCommit = !useManualInspectionApi && ShouldUseSamplingOrFullCommit();
            var isHeatTreatmentSamplingOrFull = useSamplingOrFullCommit &&
                (IsHeatTreatmentScheme(CurrentProcessName) || IsHeatTreatmentScheme(_inspectionSchemeName));
            SectionShrinkageLabel.Text = CalculateSectionShrinkageText();
            var heatReductionOfAreaRate = SectionShrinkageLabel.Text == "-"
                ? null
                : SectionShrinkageLabel.Text.TrimEnd('%');
            var picklingInspector = isAcid ? BuildCurrentRecorderUsername(RecorderEntry.Text) : string.Empty;
            if (isAcid && string.IsNullOrWhiteSpace(picklingInspector))
            {
                await DisplayAlert("提示", "当前操作人 username 为空，无法提交酸洗质检。", "确定");
                return;
            }

            var committed = useManualInspectionApi
                ? await _qualityApi.SaveManualInspectionResultAsync(new ProductionManualInspectionSaveResultRequestDto
                {
                    actualDiameterMm = HeatTreatmentInputPanel.IsVisible ? HeatActualDiameterEntry.Text?.Trim() : ActualDiameterEntry.Text?.Trim(),
                    coilDiameterControl = CoilDiameterPicker.SelectedItem?.ToString(),
                    coilPitchControl = CoilPitchPicker.SelectedItem?.ToString(),
                    elongationRate = HeatTreatmentInputPanel.IsVisible ? HeatElongationEntry.Text?.Trim() : ElongationEntry.Text?.Trim(),
                    inspectResult = InspectResultPicker.SelectedItem?.ToString(),
                    memo = MemoEditor.Text?.Trim(),
                    qrCode = _qrCode,
                    qualityMaterialId = _qualityMaterialId,
                    qualityNo = _qualityNo,
                    strengthMpa = HeatTreatmentInputPanel.IsVisible ? TensileStrengthEntry.Text?.Trim() : StrengthEntry.Text?.Trim(),
                    surfaceCondition = SurfaceEntry.Text?.Trim(),
                    workOrderNo = _workOrderNo
                })
                : isAcid
                ? await _qualityApi.CommitProductionPicklingAsync(new ProductionPicklingCommitRequestDto
                {
                    acidRatio = AcidRatioEntry.Text?.Trim(),
                    freeAcid = FreeAcidEntry.Text?.Trim(),
                    freeAcidSampling = FreeAcidSamplingEntry.Text?.Trim(),
                    hydrochloricAcidConcentration1 = HydrochloricAcid1Entry.Text?.Trim(),
                    hydrochloricAcidConcentration2 = HydrochloricAcid2Entry.Text?.Trim(),
                    inspectDate = AcidDatePicker.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    inspectResult = InspectResultPicker.SelectedItem?.ToString(),
                    inspecter = picklingInspector,
                    memo = MemoEditor.Text?.Trim(),
                    phosphatingTemperature = PhosphatingTemperatureEntry.Text?.Trim(),
                    qualityNo = _qualityNo,
                    saponificationPhValue = SaponificationPhEntry.Text?.Trim(),
                    saponificationTemperature = SaponificationTemperatureEntry.Text?.Trim(),
                    totalAcid = TotalAcidEntry.Text?.Trim(),
                    totalAcidSampling = TotalAcidSamplingEntry.Text?.Trim(),
                    workOrderNo = _workOrderNo
                })
                : useSamplingOrFullCommit
                    ? await _qualityApi.CommitProductionSamplingOrFullAsync(new ProductionSamplingOrFullCommitRequestDto
                    {
                        actualDiameterMm = HeatTreatmentInputPanel.IsVisible ? HeatActualDiameterEntry.Text?.Trim() : ActualDiameterEntry.Text?.Trim(),
                        brokenDiameter = isHeatTreatmentSamplingOrFull ? BrokenDiameterEntry.Text?.Trim() : null,
                        coilDiameterControl = CoilDiameterPicker.SelectedItem?.ToString(),
                        coilPitchControl = CoilPitchPicker.SelectedItem?.ToString(),
                        elongationRate = HeatTreatmentInputPanel.IsVisible ? HeatElongationEntry.Text?.Trim() : ElongationEntry.Text?.Trim(),
                        inspectResult = InspectResultPicker.SelectedItem?.ToString(),
                        memo = MemoEditor.Text?.Trim(),
                        qrCode = _qrCode,
                        qualityMaterialId = _qualityMaterialId,
                        qualityNo = _qualityNo,
                        reductionOfAreaRate = isHeatTreatmentSamplingOrFull ? heatReductionOfAreaRate : null,
                        strengthMpa = HeatTreatmentInputPanel.IsVisible ? TensileStrengthEntry.Text?.Trim() : StrengthEntry.Text?.Trim(),
                        surfaceCondition = SurfaceEntry.Text?.Trim(),
                        torsion = isHeatTreatmentSamplingOrFull ? TwistCountEntry.Text?.Trim() : null,
                        workOrderNo = _workOrderNo
                    })
                    : useFirstInspectionCommit
                    ? await _qualityApi.CommitProductionFirstInspectionAsync(new ProductionFirstInspectionCommitRequestDto
                    {
                        actualDiameterMm = ActualDiameterEntry.Text?.Trim(),
                        coilDiameterControl = CoilDiameterPicker.SelectedItem?.ToString(),
                        coilPitchControl = CoilPitchPicker.SelectedItem?.ToString(),
                        elongationRate = ElongationEntry.Text?.Trim(),
                        inspectResult = InspectResultPicker.SelectedItem?.ToString(),
                        memo = MemoEditor.Text?.Trim(),
                        qualityNo = _qualityNo,
                        strengthMpa = StrengthEntry.Text?.Trim(),
                        surfaceCondition = SurfaceEntry.Text?.Trim(),
                        workOrderNo = _workOrderNo
                    })
                    : await _qualityApi.CommitProductionQualityAsync(request);
            if (!committed)
            {
                await ErrorDialogService.ShowAsync(this, isAcid ? "完成失败" : "提交失败", isAcid ? "接口未返回完成成功，请稍后重试。" : "接口未返回提交成功，请稍后重试。", "确定");
                return;
            }

            await RefreshAfterSuccessfulOperationAsync();

            if (isAcid)
            {
                await Shell.Current.GoToAsync(AppShell.RouteProductionQualitySuccess);
                return;
            }

            await DisplayAlert("提交成功", "质检结果已提交。", "确定");
            if (!shouldStayAfterSubmit)
            {
                await Shell.Current.GoToAsync("..");
            }
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "提交失败", ex.Message, "确定");
        }
    }

    private async void OnCompleteClicked(object sender, EventArgs e)
    {
        var useManualInspectionApi = ShouldUseManualInspectionResultApi();
        var useSamplingOrFullComplete = ShouldUseSamplingOrFullComplete();
        if (string.IsNullOrWhiteSpace(_qualityNo) || (!useManualInspectionApi && string.IsNullOrWhiteSpace(_workOrderNo)))
        {
            var message = useManualInspectionApi
                ? "质检单号为空，无法完成。"
                : "质检单号或工单号为空，无法完成。";
            await DisplayAlert("提示", message, "确定");
            return;
        }

        try
        {
            if (useSamplingOrFullComplete)
            {
                var completed = await _qualityApi.CompleteProductionSamplingOrFullAsync(new ProductionSamplingOrFullCompleteRequestDto
                {
                    qualityNo = _qualityNo,
                    workOrderNo = _workOrderNo
                });
                if (!completed)
                {
                    await ErrorDialogService.ShowAsync(this, "完成失败", "接口未返回完成成功，请稍后重试。", "确定");
                    return;
                }
            }
            else if (useManualInspectionApi)
            {
                var completed = await _qualityApi.CompleteManualInspectionAsync(_qualityNo);
                if (!completed)
                {
                    await ErrorDialogService.ShowAsync(this, "完成失败", "接口未返回完成成功，请稍后重试。", "确定");
                    return;
                }
            }
            else
            {
                var completed = await _qualityApi.CompleteProductionSamplingOrFullAsync(new ProductionSamplingOrFullCompleteRequestDto
                {
                    qualityNo = _qualityNo,
                    workOrderNo = _workOrderNo
                });
                if (!completed)
                {
                    await ErrorDialogService.ShowAsync(this, "完成失败", "接口未返回完成成功，请稍后重试。", "确定");
                    return;
                }
            }

            await DisplayAlert("完成成功", "质检任务已完成。", "确定");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "完成失败", ex.Message, "确定");
        }
    }
}
