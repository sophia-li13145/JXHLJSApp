using JXHLJSApp.Models.Quality;
using JXHLJSApp.Pages;
using JXHLJSApp.Services.Quality;

namespace JXHLJSApp.Pages.Quality;

[QueryProperty(nameof(QualityNo), "qualityNo")]
[QueryProperty(nameof(WorkOrderNo), "workOrderNo")]
public partial class MachineQualityDetailPage : ContentPage
{
    private const string SchemeAcidPickling = "酸洗";
    private const string SchemeBlankOpening = "开胚";
    private const string SchemeHeatTreatment = "热处理";
    private const string SchemeDrawing = "拉拔";
    private readonly IQualityApi _qualityApi;
    private string? _qualityNo;
    private string? _workOrderNo;
    private string? _inspectionSchemeName;
    private string? _qrCode;
    private string? _qualityMaterialId;

    public string? QualityNo { get => _qualityNo; set => _qualityNo = Uri.UnescapeDataString(value ?? string.Empty); }
    public string? WorkOrderNo { get => _workOrderNo; set => _workOrderNo = Uri.UnescapeDataString(value ?? string.Empty); }

    public MachineQualityDetailPage(IQualityApi qualityApi)
    {
        InitializeComponent();
        _qualityApi = qualityApi;
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
        if (string.IsNullOrWhiteSpace(_qualityNo) || string.IsNullOrWhiteSpace(_workOrderNo))
        {
            await DisplayAlert("提示", "质检单号或工单号为空。", "确定");
            return;
        }

        try
        {
            var detail = await _qualityApi.GetProductionQualityDetailAsync(_qualityNo, _workOrderNo);
            _inspectionSchemeName = detail.inspectionSchemeName?.Trim();
            _qrCode = detail.qrCode;
            _qualityMaterialId = detail.qualityMaterialId;
            ApplySchemeLayout(detail);
            RenderInfo(detail);
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
        }
        catch (Exception ex)
        {
            await DisplayAlert("加载失败", ex.Message, "确定");
        }
    }

    private void ApplySchemeLayout(ProductionQualityDetailDto detail)
    {
        var schemeName = detail.inspectionSchemeName?.Trim();
        var isAcid = string.Equals(schemeName, SchemeAcidPickling, StringComparison.Ordinal);
        var isHeat = IsHeatTreatmentScheme(schemeName);

        TitleLabel.Text = schemeName switch
        {
            SchemeAcidPickling => "执行酸洗质检",
            SchemeHeatTreatment or "全检" => "执行热处理质检",
            SchemeBlankOpening or SchemeDrawing => "执行工序质检",
            _ => "执行工序质检"
        };
        InfoTitleLabel.Text = schemeName switch
        {
            SchemeAcidPickling => "酸洗任务信息",
            SchemeHeatTreatment or "全检" => "热处理卡片信息",
            _ => "生产卡片信息"
        };
        InputTitleLabel.Text = isAcid ? "酸洗检验录入" : isHeat ? "理化检验录入" : "检验项目录入";
        AcidInputPanel.IsVisible = isAcid;
        HeatTreatmentInputPanel.IsVisible = isHeat;
        ProcessInputPanel.IsVisible = !isAcid && !isHeat;
        MemoLabel.IsVisible = !isHeat;
        MemoEditor.IsVisible = !isHeat;
        CompleteButton.IsVisible = IsSamplingOrFullScheme(schemeName);
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

    private static (string Label, string? Value)[] BuildInfoRows(ProductionQualityDetailDto detail)
    {
        var schemeName = detail.inspectionSchemeName?.Trim();
        if (IsPicklingScheme(schemeName))
        {
            return new[]
            {
                ("检验日期", DateTime.Now.ToString("yyyy-MM-dd")), ("工单号", detail.workOrderNo)
            };
        }

        if (IsHeatTreatmentScheme(schemeName))
        {
            return new[]
            {
                ("日期", DateTime.Now.ToString("yyyyMMdd")), ("机台号", detail.deviceName ?? detail.deviceCode),
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
                ("工号", detail.workOrderNo), ("件号", detail.targetSpecification),
                ("产地", FirstNonEmpty(detail.originPlace, detail.freeAcid)), ("投料直径mm", detail.inputDiameterMm),
                ("成品直径mm", detail.productDiameter), ("上公差", detail.upperToleranceValue),
                ("下公差", detail.lowerToleranceValue), ("强度要求", detail.spoolWeightRequirement),
                ("圈径", detail.coilDiameterControl), ("圈径控制", detail.coilDiameterControl),
                ("圈距控制", detail.coilPitchControl)
            };
        }

        return new[] { ("日期", DateTime.Now.ToString("yyyyMMdd")), ("工单号", detail.workOrderNo), ("质检方案", detail.inspectionSchemeName) };
    }

    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;

    private static View CreateInfoCell(string label, string? value)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = GridLength.Star }),
            ColumnSpacing = 10
        };
        grid.Add(new Label { Text = label, TextColor = Color.FromArgb("#38557C"), FontSize = 14 });
        grid.Add(new Label { Text = string.IsNullOrWhiteSpace(value) ? "-" : value, TextColor = Color.FromArgb("#0042AD"), FontSize = 14, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.End }, 1);
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

            var committed = IsPicklingScheme(_inspectionSchemeName)
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
                await DisplayAlert("提交失败", "接口未返回提交成功，请稍后重试。", "确定");
                return;
            }

            await DisplayAlert("提交成功", "质检结果已提交。", "确定");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await DisplayAlert("提交失败", ex.Message, "确定");
        }
    }

    private async void OnCompleteClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_qualityNo) || string.IsNullOrWhiteSpace(_workOrderNo))
        {
            await DisplayAlert("提示", "质检单号或工单号为空，无法完成。", "确定");
            return;
        }

        try
        {
            var completed = await _qualityApi.CompleteProductionSamplingOrFullAsync(new ProductionSamplingOrFullCompleteRequestDto
            {
                qualityNo = _qualityNo,
                workOrderNo = _workOrderNo
            });
            if (!completed)
            {
                await DisplayAlert("完成失败", "接口未返回完成成功，请稍后重试。", "确定");
                return;
            }

            await DisplayAlert("完成成功", "质检任务已完成。", "确定");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await DisplayAlert("完成失败", ex.Message, "确定");
        }
    }
}
