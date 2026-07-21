using JXHLJSApp.Models.Quality;
using JXHLJSApp.Pages;
using JXHLJSApp.Services;
using JXHLJSApp.Services.Quality;
using Microsoft.Maui.Controls.Shapes;

namespace JXHLJSApp.Pages.Quality;

[QueryProperty(nameof(QualityNo), "qualityNo")]
[QueryProperty(nameof(WorkOrderNo), "workOrderNo")]
[QueryProperty(nameof(InspectStatus), "inspectStatus")]
[QueryProperty(nameof(ManualInspection), "manualInspection")]
[QueryProperty(nameof(ProcessName), "processName")]
public partial class MachineQualityDetailPage : ContentPage
{
    private const string SchemeAcidPickling = "酸洗";
    private const string SchemeBlankOpening = "开坯";
    private const string SchemeHeatTreatment = "热处理";
    private const string SchemeDrawing = "拉拔";
    private readonly IQualityApi _qualityApi;
    private readonly IScanService _scanService;
    private ProductionQualityDetailDto? _detail;
    private string? _qualityNo;
    private string? _workOrderNo;
    private string? _inspectionSchemeName;
    private string? _inspectStatus;
    private string? _processNameFromScan;
    private string? _qrCode;
    private string? _qualityMaterialId;
    private bool _isManualInspection;
    private bool _manualInspectionFromQuery;

    public string? QualityNo { get => _qualityNo; set => _qualityNo = Uri.UnescapeDataString(value ?? string.Empty); }
    public string? WorkOrderNo { get => _workOrderNo; set => _workOrderNo = Uri.UnescapeDataString(value ?? string.Empty); }
    public string? InspectStatus { get => _inspectStatus; set => _inspectStatus = Uri.UnescapeDataString(value ?? string.Empty); }
    public string? ManualInspection { get => _manualInspectionFromQuery ? "true" : "false"; set => _manualInspectionFromQuery = string.Equals(Uri.UnescapeDataString(value ?? string.Empty), "true", StringComparison.OrdinalIgnoreCase); }
    public string? ProcessName { get => _processNameFromScan; set => _processNameFromScan = Uri.UnescapeDataString(value ?? string.Empty); }

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
        await LoadAsync();
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
            _isManualInspection = _manualInspectionFromQuery || string.IsNullOrWhiteSpace(_workOrderNo);
            var detail = _isManualInspection
                ? await _qualityApi.GetManualInspectionDetailAsync(_qualityNo)
                : await _qualityApi.GetProductionQualityDetailAsync(_qualityNo, _workOrderNo);
            _detail = detail;
            ApplyScannedProcessNameFallback(detail);
            _inspectionSchemeName = ResolveProcessName(detail);
            if (!string.IsNullOrWhiteSpace(detail.inspectStatus)) _inspectStatus = detail.inspectStatus;
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
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "加载失败", ex.Message, "确定");
        }
    }

    private void ApplySchemeLayout(ProductionQualityDetailDto detail)
    {
        var processName = ResolveProcessName(detail);
        var isAcid = IsPicklingScheme(processName);
        var isHeat = IsHeatTreatmentScheme(processName);
        var isDrawing = IsDrawingScheme(processName);

        TitleLabel.Text = isAcid ? "执行酸洗质检" : isHeat ? "执行热处理质检" : "执行工序质检";
        InfoTitleLabel.Text = isAcid ? "酸洗任务信息" : isHeat ? "热处理卡片信息" : "生产卡片信息";
        InputTitleLabel.Text = isAcid ? "酸洗检验录入" : isHeat ? "理化检验录入" : "检验项目录入";
        AcidInputPanel.IsVisible = isAcid;
        HeatTreatmentInputPanel.IsVisible = isHeat;
        ProcessInputPanel.IsVisible = !isAcid && !isHeat;
        MemoLabel.IsVisible = !isHeat;
        MemoEditor.IsVisible = !isHeat;
        var isSubmitOnlyProcess = isAcid || isDrawing;
        CompleteButton.IsVisible = !isSubmitOnlyProcess;
        Grid.SetColumnSpan(SubmitButton, isSubmitOnlyProcess ? 2 : 1);
        ScanMaterialButton.IsVisible = false;
        InfoScanMaterialButton.IsVisible = IsSamplingOrFullScheme(processName) || IsProcessCardScheme(processName);
    }

    private void FillAcidInputs(ProductionQualityDetailDto detail)
    {
        AcidDateEntry.Text = DateTime.Now.ToString("yyyy/MM/dd");
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
        RecorderEntry.Text = $"{Preferences.Get(UserSessionKeys.RealName, string.Empty)} ({Preferences.Get(UserSessionKeys.WorkNumber, string.Empty)})".Trim();
    }

    private void FillHeatTreatmentInputs(ProductionQualityDetailDto detail)
    {
        HeatActualDiameterEntry.Text = detail.actualDiameterMm;
        StandardDiameterEntry.Text = FirstNonEmpty(detail.standardDiameterMm, detail.productDiameter);
        BrokenDiameterEntry.Text = detail.brokenDiameterMm;
        SectionShrinkageLabel.Text = string.IsNullOrWhiteSpace(detail.sectionShrinkageRate) ? CalculateSectionShrinkageText() : detail.sectionShrinkageRate;
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
        return FirstNonEmpty(detail.processName, detail.processCode, detail.inspectionSchemeName, detail.qualityTypeName, detail.inspectionSchemeTypeName).Trim();
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
                ("日期", DateTime.Now.ToString("yyyyMMdd")), ("机台", detail.deviceName ?? detail.deviceCode),
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
                ("日期", DateTime.Now.ToString("yyyyMMdd")), ("机台", detail.deviceName ?? detail.deviceCode),
                ("客户代码", FirstNonEmpty(detail.customerCode, detail.businessType)), ("炉号", detail.furnaceNo),
                ("钢号", detail.steelGrade), ("挂牌", detail.inputSpecification),
                ("工号", detail.workOrderNo), ("件号", ResolvePieceNo(detail)),
                ("产地", FirstNonEmpty(detail.originPlace, detail.freeAcid)), ("投料直径mm", detail.inputDiameterMm),
                ("成品直径mm", detail.productDiameter), ("上公差", detail.upperToleranceValue),
                ("下公差", detail.lowerToleranceValue), ("强度要求", detail.spoolWeightRequirement),
                ("圈径", detail.coilDiameterControl), ("圈径控制", detail.coilDiameterControl),
                ("圈距控制", detail.coilPitchControl)
            };
        }

        return new[]
        {
            ("日期", DateTime.Now.ToString("yyyyMMdd")), ("巡检单号", detail.qualityNo),
            ("工单号", detail.workOrderNo), ("工序", FirstNonEmpty(detail.processName, detail.processCode)),
            ("质检方案", detail.inspectionSchemeName), ("方案类型", detail.inspectionSchemeTypeName),
            ("质检类型", FirstNonEmpty(detail.qualityTypeName, detail.qualityType)), ("状态", detail.inspectStatus),
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
            ("件号", material.pieceNo), ("规格", FirstNonEmpty(material.spec, material.inputSpecification, material.targetSpecification)),
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

    private static string ResolvePieceNo(ProductionQualityDetailDto detail)
    {
        return FirstNonEmpty(detail.pieceNo, detail.materialList?.FirstOrDefault()?.pieceNo);
    }

    private static string BuildLimitText(ProductionQualityInspectionItemDto item)
    {
        var lower = string.IsNullOrWhiteSpace(item.lowerLimit) ? "-" : item.lowerLimit;
        var upper = string.IsNullOrWhiteSpace(item.upperLimit) ? "-" : item.upperLimit;
        return lower == "-" && upper == "-" ? string.Empty : $"{lower} ~ {upper}";
    }

    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;

    private void ApplyReadOnlyStateIfCompleted()
    {
        if (!IsInspectionCompleted(_inspectStatus)) return;

        SetReadOnly(Content);
        ActionBar.IsVisible = false;
    }

    private static bool IsInspectionCompleted(string? inspectStatus)
    {
        return string.Equals(inspectStatus, "3", StringComparison.Ordinal) ||
            string.Equals(inspectStatus, "检验完成", StringComparison.Ordinal) ||
            string.Equals(inspectStatus, "已完成", StringComparison.Ordinal);
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
            ColumnSpacing = 10
        };
        var labelColor = Color.FromArgb("#38557C");
        var valueColor = Color.FromArgb("#0042AD");
        grid.Add(new Label { Text = label, TextColor = labelColor, FontSize = 14 });
        grid.Add(new Label { Text = string.IsNullOrWhiteSpace(value) ? "-" : value, TextColor = valueColor, FontSize = 14, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.End }, 1);
        return grid;
    }

    private static void SelectQualifiedOption(Picker picker, string? value)
    {
        picker.SelectedItem = string.Equals(value, "不合格", StringComparison.Ordinal) ? "不合格" : "合格";
    }

    private static bool IsPicklingScheme(string? schemeName)
    {
        return HasSchemeToken(schemeName, SchemeAcidPickling, "表检");
    }

    private static bool IsHeatTreatmentScheme(string? schemeName)
    {
        return HasSchemeToken(schemeName, SchemeHeatTreatment, "全检");
    }

    private static bool IsSamplingOrFullScheme(string? schemeName)
    {
        return HasSchemeToken(schemeName, SchemeBlankOpening, "抽检") || IsHeatTreatmentScheme(schemeName);
    }

    private static bool IsProcessCardScheme(string? schemeName)
    {
        return HasSchemeToken(schemeName, SchemeBlankOpening, SchemeDrawing, "抽检") || IsFirstInspectionScheme(schemeName);
    }

    private static bool IsDrawingScheme(string? schemeName)
    {
        return HasSchemeToken(schemeName, SchemeDrawing);
    }

    private static bool IsFirstInspectionScheme(string? schemeName)
    {
        return HasSchemeToken(schemeName, SchemeDrawing, "首检", "首件检");
    }

    private static bool HasSchemeToken(string? schemeName, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(schemeName)) return false;
        return tokens.Any(token => schemeName.Contains(token, StringComparison.Ordinal));
    }

    private void OnHeatDiameterTextChanged(object sender, TextChangedEventArgs e)
    {
        SectionShrinkageLabel.Text = CalculateSectionShrinkageText();
    }

    private string CalculateSectionShrinkageText()
    {
        if (!decimal.TryParse(HeatActualDiameterEntry.Text, out var actualDiameter) ||
            !decimal.TryParse(BrokenDiameterEntry.Text, out var brokenDiameter) ||
            actualDiameter <= 0 || brokenDiameter < 0 || brokenDiameter > actualDiameter)
        {
            return "-";
        }

        var rate = (1 - (brokenDiameter * brokenDiameter) / (actualDiameter * actualDiameter)) * 100;
        return $"{rate:F2}%";
    }


    private async void OnScanMaterialClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_qualityNo) || string.IsNullOrWhiteSpace(_workOrderNo))
        {
            await DisplayAlert("提示", "质检单号或工单号为空，无法扫码物料。", "确定");
            return;
        }

        var code = await _scanService.ScanAsync("生产质检扫码物料");
        if (string.IsNullOrWhiteSpace(code)) return;

        try
        {
            var material = await _qualityApi.ScanProductionQualityMaterialAsync(new ProductionQualityScanMaterialRequestDto
            {
                qrCode = code,
                qualityNo = _qualityNo,
                workOrderNo = _workOrderNo
            });
            ApplyScannedMaterial(material, code);
            await DisplayAlert("扫码成功", "物料信息已更新到当前质检页面。", "确定");
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "扫码物料失败", ex.Message, "确定");
        }
    }

    private void ApplyScannedMaterial(ProductionQualityScanMaterialDto material, string fallbackQrCode)
    {
        _qrCode = FirstNonEmpty(material.qrCode, fallbackQrCode);
        _qualityMaterialId = FirstNonEmpty(material.qualityMaterialId, _qualityMaterialId);
        if (_detail is null) _detail = new ProductionQualityDetailDto { workOrderNo = _workOrderNo };

        _detail.batchNo = FirstNonEmpty(material.batchNo, _detail.batchNo);
        _detail.businessType = FirstNonEmpty(material.businessType, _detail.businessType);
        _detail.customerCode = FirstNonEmpty(material.customerCode, _detail.customerCode);
        _detail.deviceCode = FirstNonEmpty(material.deviceCode, _detail.deviceCode);
        _detail.deviceName = FirstNonEmpty(material.deviceName, _detail.deviceName);
        _detail.furnaceNo = FirstNonEmpty(material.furnaceNo, _detail.furnaceNo);
        _detail.inputDiameterMm = FirstNonEmpty(material.inputDiameterMm, _detail.inputDiameterMm);
        _detail.inputSpecification = FirstNonEmpty(material.inputSpecification, _detail.inputSpecification);
        _detail.originPlace = FirstNonEmpty(material.originPlace, _detail.originPlace);
        _detail.plateNo = FirstNonEmpty(material.plateNo, _detail.plateNo);
        _detail.productDiameter = FirstNonEmpty(material.productDiameter, _detail.productDiameter);
        _detail.qrCode = _qrCode;
        _detail.qualityMaterialId = _qualityMaterialId;
        _detail.shiftNo = FirstNonEmpty(material.shiftNo, _detail.shiftNo);
        _detail.steelGrade = FirstNonEmpty(material.steelGrade, _detail.steelGrade);
        _detail.targetSpecification = FirstNonEmpty(material.targetSpecification, _detail.targetSpecification);
        _detail.upperToleranceValue = FirstNonEmpty(material.upperToleranceValue, _detail.upperToleranceValue);
        _detail.lowerToleranceValue = FirstNonEmpty(material.lowerToleranceValue, _detail.lowerToleranceValue);
        _detail.spoolWeightRequirement = FirstNonEmpty(material.spoolWeightRequirement, _detail.spoolWeightRequirement);
        _detail.workOrderNo = FirstNonEmpty(material.workOrderNo, _detail.workOrderNo);

        RenderInfo(_detail);
        RenderMaterialInfo(_detail);
        RenderInspectionItems(_detail);
        StandardDiameterEntry.Text = FirstNonEmpty(_detail.standardDiameterMm, _detail.productDiameter);
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

            var committed = _isManualInspection
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
                : IsPicklingScheme(_inspectionSchemeName)
                ? await _qualityApi.CommitProductionPicklingAsync(new ProductionPicklingCommitRequestDto
                {
                    acidRatio = AcidRatioEntry.Text?.Trim(),
                    freeAcid = FreeAcidEntry.Text?.Trim(),
                    freeAcidSampling = FreeAcidSamplingEntry.Text?.Trim(),
                    hydrochloricAcidConcentration1 = HydrochloricAcid1Entry.Text?.Trim(),
                    hydrochloricAcidConcentration2 = HydrochloricAcid2Entry.Text?.Trim(),
                    inspectDate = AcidDateEntry.Text?.Trim(),
                    inspectResult = InspectResultPicker.SelectedItem?.ToString(),
                    inspector = RecorderEntry.Text?.Trim(),
                    memo = MemoEditor.Text?.Trim(),
                    phosphatingTemperature = PhosphatingTemperatureEntry.Text?.Trim(),
                    qualityNo = _qualityNo,
                    saponificationPhValue = SaponificationPhEntry.Text?.Trim(),
                    saponificationTemperature = SaponificationTemperatureEntry.Text?.Trim(),
                    totalAcid = TotalAcidEntry.Text?.Trim(),
                    totalAcidSampling = TotalAcidSamplingEntry.Text?.Trim(),
                    workOrderNo = _workOrderNo
                })
                : IsSamplingOrFullScheme(_inspectionSchemeName)
                    ? await _qualityApi.CommitProductionSamplingOrFullAsync(new ProductionSamplingOrFullCommitRequestDto
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
                    : IsFirstInspectionScheme(_inspectionSchemeName)
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
                await ErrorDialogService.ShowAsync(this, "提交失败", "接口未返回提交成功，请稍后重试。", "确定");
                return;
            }

            await DisplayAlert("提交成功", "质检结果已提交。", "确定");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "提交失败", ex.Message, "确定");
        }
    }

    private async void OnCompleteClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_qualityNo) || (!_isManualInspection && string.IsNullOrWhiteSpace(_workOrderNo)))
        {
            await DisplayAlert("提示", _isManualInspection ? "质检单号为空，无法完成。" : "质检单号或工单号为空，无法完成。", "确定");
            return;
        }

        try
        {
            if (_isManualInspection)
            {
                await _qualityApi.CompleteManualInspectionAsync(_qualityNo);
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
