using JXHLJSApp.Models.Quality;
using JXHLJSApp.Services.Quality;

namespace JXHLJSApp.Pages.Quality;

[QueryProperty(nameof(QualityNo), "qualityNo")]
[QueryProperty(nameof(WorkOrderNo), "workOrderNo")]
public partial class MachineQualityDetailPage : ContentPage
{
    private readonly IQualityApi _qualityApi;
    private string? _qualityNo;
    private string? _workOrderNo;

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
            RenderInfo(detail);
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

    private void RenderInfo(ProductionQualityDetailDto detail)
    {
        InfoGrid.Children.Clear();
        InfoGrid.RowDefinitions.Clear();
        var rows = new (string Label, string? Value)[]
        {
            ("日期", DateTime.Now.ToString("yyyyMMdd")), ("机台", detail.deviceName ?? detail.deviceCode),
            ("客户代码", detail.businessType), ("炉号", detail.furnaceNo),
            ("钢号", detail.steelGrade), ("挂脾", detail.inputSpecification),
            ("工号", detail.workOrderNo), ("件号", detail.targetSpecification),
            ("产地", detail.freeAcid), ("投料直径mm", detail.inputDiameterMm),
            ("成品直径mm", detail.productDiameter), ("上公差", detail.upperToleranceValue),
            ("下公差", detail.lowerToleranceValue), ("强度要求", detail.spoolWeightRequirement),
            ("圈径", detail.coilDiameterControl), ("圈径控制", detail.coilDiameterControl),
            ("圈距控制", detail.coilPitchControl), ("质检方案", detail.inspectionSchemeName)
        };

        for (var i = 0; i < rows.Length; i++)
        {
            if (i % 2 == 0) InfoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var cell = CreateInfoCell(rows[i].Label, rows[i].Value);
            InfoGrid.Add(cell, i % 2, i / 2);
        }
    }

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
            };

            var committed = await _qualityApi.CommitProductionQualityAsync(request);
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
}
