using JXHLJSApp.Models.Quality;
using JXHLJSApp.Services.Quality;

namespace JXHLJSApp.Pages.Quality;

[QueryProperty(nameof(IncomingQualityNo), "incomingQualityNo")]
[QueryProperty(nameof(QrCode), "qrCode")]
public partial class IncomingQualityScanPage : ContentPage
{
    private readonly IQualityApi _qualityApi;
    private string? _incomingQualityNo;
    private string? _qrCode;
    private List<QualityDictOption> _problemOptions = new();
    private IncomingQualityScanMaterialDto? _scanMaterial;

    public string? IncomingQualityNo
    {
        get => _incomingQualityNo;
        set => _incomingQualityNo = Uri.UnescapeDataString(value ?? string.Empty);
    }

    public string? QrCode
    {
        get => _qrCode;
        set => _qrCode = Uri.UnescapeDataString(value ?? string.Empty);
    }

    public IncomingQualityScanPage(IQualityApi qualityApi)
    {
        InitializeComponent();
        _qualityApi = qualityApi;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadScanFormAsync();
    }

    private async Task LoadScanFormAsync()
    {
        try
        {
            var inspectResults = await _qualityApi.GetInspectResultOptionsAsync();
            _problemOptions = await _qualityApi.GetProblemPointOptionsAsync();
            InspectResultPicker.ItemsSource = inspectResults;
            var defaultIndex = inspectResults.FindIndex(item => item.Value.Contains("不合格", StringComparison.OrdinalIgnoreCase) || item.Name.Contains("不合格"));
            if (inspectResults.Count > 0)
            {
                InspectResultPicker.SelectedIndex = defaultIndex >= 0 ? defaultIndex : 0;
            }

            if (!string.IsNullOrWhiteSpace(_qrCode))
            {
                _scanMaterial = await _qualityApi.ScanIncomingQualityMaterialAsync(_qrCode);
                QrCodeLabel.Text = _scanMaterial.qrCodeDisplay;
                MaterialHintLabel.Text = _scanMaterial.materialDisplay == "-" ? "未提交单据无法获取物料明细" : _scanMaterial.materialDisplay;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("加载失败", ex.Message, "确定");
        }
    }

    private async void OnProblemPointTapped(object sender, TappedEventArgs e)
    {
        if (_problemOptions.Count == 0)
        {
            await DisplayAlert("提示", "暂无可选问题点。", "确定");
            return;
        }

        var names = _problemOptions.Select(item => item.Name).ToArray();
        var selected = await DisplayActionSheet("选择问题点", "取消", null, names);
        if (string.IsNullOrWhiteSpace(selected) || selected == "取消") return;

        foreach (var option in _problemOptions)
        {
            option.IsSelected = option.Name == selected;
        }
        ProblemPointLabel.Text = selected;
        ProblemPointLabel.TextColor = Color.FromArgb("#051B3D");
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (InspectResultPicker.SelectedItem is not QualityDictOption inspectResult)
        {
            await DisplayAlert("提示", "请选择质检结果。", "确定");
            return;
        }

        var selectedProblem = _problemOptions.FirstOrDefault(item => item.IsSelected);
        if (selectedProblem is null)
        {
            await DisplayAlert("提示", "请选择问题点。", "确定");
            return;
        }

        if (_scanMaterial is null)
        {
            await DisplayAlert("提示", "未获取到扫码物料信息，无法保存。", "确定");
            return;
        }

        try
        {
            var saved = await _qualityApi.SaveIncomingQualityResultAsync(new IncomingQualitySaveResultRequestDto
            {
                inspectResult = inspectResult.Value,
                instockNo = _scanMaterial.instockNo ?? string.Empty,
                materialCode = _scanMaterial.materialCode ?? string.Empty,
                materialName = _scanMaterial.materialName ?? string.Empty,
                otherExceptionDesc = RemarkEditor.Text?.Trim(),
                otherProblemItem = OtherProblemEditor.Text?.Trim(),
                problemPoint = selectedProblem.Value,
                qrCode = _scanMaterial.qrCode ?? _qrCode ?? string.Empty
            });

            if (!saved)
            {
                await DisplayAlert("保存失败", "接口未返回保存成功，请稍后重试。", "确定");
                return;
            }

            await DisplayAlert("保存成功", "质检结果已保存。", "确定");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await DisplayAlert("保存失败", ex.Message, "确定");
        }
    }

    private async void OnCancelTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
