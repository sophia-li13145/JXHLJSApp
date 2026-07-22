using JXHLJSApp.Models.Quality;
using JXHLJSApp.Services;
using JXHLJSApp.Services.Quality;

namespace JXHLJSApp.Pages.Quality;

[QueryProperty(nameof(IncomingQualityNo), "incomingQualityNo")]
[QueryProperty(nameof(QrCode), "qrCode")]
public partial class IncomingQualityScanPage : ContentPage
{
    private readonly IQualityApi _qualityApi;
    private readonly IScanService _scanService;
    private string? _incomingQualityNo;
    private string? _qrCode;
    private bool _hasLoadedScanForm;
    private string? _loadedQrCode;
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

    public IncomingQualityScanPage(IQualityApi qualityApi, IScanService scanService)
    {
        InitializeComponent();
        _qualityApi = qualityApi;
        _scanService = scanService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_hasLoadedScanForm && string.Equals(_loadedQrCode, _qrCode, StringComparison.Ordinal))
        {
            return;
        }

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

            if (!string.IsNullOrWhiteSpace(_qrCode) && _scanMaterial is null)
            {
                await LoadScannedMaterialAsync(_qrCode);
            }
            else
            {
                ScanPanel.IsVisible = _scanMaterial is null;
                MaterialInfoCard.IsVisible = _scanMaterial is not null;
            }

            _hasLoadedScanForm = true;
            _loadedQrCode = _qrCode;
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "加载失败", ex.Message, "确定");
        }
    }

    private async void OnScanPanelTapped(object sender, TappedEventArgs e)
    {
        var code = await _scanService.ScanAsync("来料质检扫码");
        if (string.IsNullOrWhiteSpace(code)) return;
        try
        {
            await LoadScannedMaterialAsync(code.Trim());
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "扫码失败", ex.Message, "确定");
        }
    }

    private async Task LoadScannedMaterialAsync(string qrCode)
    {
        _qrCode = qrCode;
        _scanMaterial = await _qualityApi.ScanIncomingQualityMaterialAsync(qrCode);
        QrCodeLabel.Text = _scanMaterial.qrCodeDisplay;
        MaterialHintLabel.Text = _scanMaterial.materialDisplay == "-" ? "未提交单据无法获取物料明细" : _scanMaterial.materialDisplay;
        ScanPanel.IsVisible = false;
        MaterialInfoCard.IsVisible = true;
        _hasLoadedScanForm = true;
        _loadedQrCode = _qrCode;
    }

    private async void OnProblemPointTapped(object sender, TappedEventArgs e)
    {
        if (_problemOptions.Count == 0)
        {
            await DisplayAlert("提示", "暂无可选问题点。", "确定");
            return;
        }

        while (true)
        {
            var names = _problemOptions
                .Select(item => $"{(item.IsSelected ? "✓ " : string.Empty)}{item.Name}")
                .ToArray();
            var selected = await DisplayActionSheet("选择问题点（可多选）", "取消", "完成", names);
            if (string.IsNullOrWhiteSpace(selected) || selected == "取消")
            {
                return;
            }

            if (selected == "完成")
            {
                RefreshProblemPointDisplay();
                return;
            }

            var selectedName = selected.StartsWith("✓ ", StringComparison.Ordinal) ? selected[2..] : selected;
            var option = _problemOptions.FirstOrDefault(item => item.Name == selectedName);
            if (option is not null)
            {
                option.IsSelected = !option.IsSelected;
            }
        }
    }

    private void RefreshProblemPointDisplay()
    {
        var selectedNames = _problemOptions
            .Where(item => item.IsSelected)
            .Select(item => item.Name)
            .ToList();

        ProblemPointLabel.Text = selectedNames.Count == 0 ? "点击选择问题点" : string.Join("、", selectedNames);
        ProblemPointLabel.TextColor = selectedNames.Count == 0 ? Color.FromArgb("#7A889A") : Color.FromArgb("#051B3D");
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (InspectResultPicker.SelectedItem is not QualityDictOption inspectResult)
        {
            await DisplayAlert("提示", "请选择质检结果。", "确定");
            return;
        }

        var selectedProblems = _problemOptions.Where(item => item.IsSelected).ToList();
        if (selectedProblems.Count == 0)
        {
            await DisplayAlert("提示", "请选择问题点。", "确定");
            return;
        }

        if (_scanMaterial is null)
        {
            await DisplayAlert("提示", "请先扫描物料二维码。", "确定");
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
                problemPoint = string.Join(",", selectedProblems.Select(item => item.Value)),
                qrCode = _scanMaterial.qrCode ?? _qrCode ?? string.Empty
            });

            if (!saved)
            {
                await ErrorDialogService.ShowAsync(this, "保存失败", "接口未返回保存成功，请稍后重试。", "确定");
                return;
            }

            await DisplayAlert("保存成功", "质检结果已保存。", "确定");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "保存失败", ex.Message, "确定");
        }
    }

    private async void OnCancelTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
