using JXHLJSApp.Models.Quality;
using JXHLJSApp.Services;
using JXHLJSApp.Services.Quality;

namespace JXHLJSApp.Pages.Quality;

[QueryProperty(nameof(IncomingQualityNo), "incomingQualityNo")]
[QueryProperty(nameof(QrCode), "qrCode")]
public partial class IncomingQualityScanPage : ContentPage, IQueryAttributable
{
    private readonly IQualityApi _qualityApi;
    private readonly IScanService _scanService;
    private string? _incomingQualityNo;
    private string? _qrCode;
    private bool _hasLoadedScanForm;
    private string? _loadedQrCode;
    private List<QualityDictOption> _problemOptions = new();
    private List<QualityDictOption> _inspectResultOptions = new();
    private readonly HashSet<string> _pendingProblemValues = new(StringComparer.OrdinalIgnoreCase);
    private QualityDictOption? _selectedInspectResult;
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

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("scanMaterial", out var value) && value is IncomingQualityScanMaterialDto scanMaterial)
        {
            _scanMaterial = scanMaterial;
            _qrCode = scanMaterial.qrCodeDisplay == "-" ? _qrCode : scanMaterial.qrCodeDisplay;
        }
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
            _inspectResultOptions = await _qualityApi.GetInspectResultOptionsAsync();
            _problemOptions = await _qualityApi.GetProblemPointOptionsAsync();
            BuildInspectResultOptions();
            BuildProblemPointOptions();
            var defaultIndex = _inspectResultOptions.FindIndex(item => item.Value.Contains("不合格", StringComparison.OrdinalIgnoreCase) || item.Name.Contains("不合格"));
            if (_inspectResultOptions.Count > 0)
            {
                SetInspectResult(_inspectResultOptions[defaultIndex >= 0 ? defaultIndex : 0]);
            }

            if (!string.IsNullOrWhiteSpace(_qrCode) && _scanMaterial is null)
            {
                await LoadScannedMaterialAsync(_qrCode);
            }
            else if (_scanMaterial is not null)
            {
                DisplayScannedMaterial(_scanMaterial);
            }
            else
            {
                ScanPanel.IsVisible = true;
                MaterialInfoCard.IsVisible = false;
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
        DisplayScannedMaterial(_scanMaterial);
        _hasLoadedScanForm = true;
        _loadedQrCode = _qrCode;
    }

    private void DisplayScannedMaterial(IncomingQualityScanMaterialDto scanMaterial)
    {
        QrCodeLabel.Text = scanMaterial.qrCodeDisplay;
        MaterialHintLabel.Text = scanMaterial.materialDisplay == "-" ? "未提交单据无法获取物料明细" : scanMaterial.materialDisplay;
        ScanPanel.IsVisible = false;
        MaterialInfoCard.IsVisible = true;
    }

    private void BuildInspectResultOptions()
    {
        InspectResultOptionsLayout.Clear();
        foreach (var option in _inspectResultOptions)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new(GridLength.Auto),
                    new(GridLength.Star)
                },
                Padding = new Thickness(20, 0),
                HeightRequest = 29,
                BackgroundColor = Colors.White,
                BindingContext = option
            };
            row.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => SetInspectResult(option, true)) });
            row.Add(new Label
            {
                Text = GetInspectResultIcon(option),
                TextColor = GetInspectResultColor(option),
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                VerticalTextAlignment = TextAlignment.Center
            }, 0);
            row.Add(new Label
            {
                Text = option.Name,
                TextColor = GetInspectResultColor(option),
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalTextAlignment = TextAlignment.Center
            }, 1);
            InspectResultOptionsLayout.Add(row);
        }
    }

    private void SetInspectResult(QualityDictOption option, bool closeDropdown = false)
    {
        _selectedInspectResult = option;
        InspectResultIconLabel.Text = GetInspectResultIcon(option);
        InspectResultIconLabel.TextColor = GetInspectResultColor(option);
        InspectResultLabel.Text = option.Name;
        InspectResultLabel.TextColor = GetInspectResultColor(option);

        foreach (var row in InspectResultOptionsLayout.Children.OfType<Grid>())
        {
            row.BackgroundColor = ReferenceEquals(row.BindingContext, option) ? Color.FromArgb("#7F7F7F") : Colors.White;
        }

        if (closeDropdown)
        {
            InspectResultDropdown.IsVisible = false;
        }
    }

    private static string GetInspectResultIcon(QualityDictOption option) =>
        IsQualifiedResult(option) ? "☑" : "✕";

    private static Color GetInspectResultColor(QualityDictOption option) =>
        IsQualifiedResult(option) ? Color.FromArgb("#16A34A") : Color.FromArgb("#FF3B4E");

    private static bool IsQualifiedResult(QualityDictOption option) =>
        option.Name.Contains("合格") && !option.Name.Contains("不合格") ||
        option.Value.Contains("合格") && !option.Value.Contains("不合格");

    private void OnInspectResultTapped(object sender, TappedEventArgs e)
    {
        InspectResultDropdown.IsVisible = !InspectResultDropdown.IsVisible;
    }

    private async void OnProblemPointTapped(object sender, TappedEventArgs e)
    {
        if (_problemOptions.Count == 0)
        {
            await DisplayAlert("提示", "暂无可选问题点。", "确定");
            return;
        }

        _pendingProblemValues.Clear();
        foreach (var option in _problemOptions.Where(item => item.IsSelected))
        {
            _pendingProblemValues.Add(option.Value);
        }

        RefreshProblemPointOptionChecks();
        ProblemPointSheetOverlay.IsVisible = true;
    }

    private void BuildProblemPointOptions()
    {
        ProblemPointOptionsLayout.Clear();
        foreach (var option in _problemOptions)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new(GridLength.Auto),
                    new(GridLength.Star)
                },
                ColumnSpacing = 12,
                HeightRequest = 55,
                Padding = new Thickness(4, 0),
                BindingContext = option
            };
            row.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => ToggleProblemPoint(option)) });

            var checkBox = new Border
            {
                WidthRequest = 22,
                HeightRequest = 22,
                Stroke = Color.FromArgb("#8C98AA"),
                StrokeThickness = 1,
                BackgroundColor = Colors.White,
                VerticalOptions = LayoutOptions.Center,
                Content = new Label
                {
                    Text = _pendingProblemValues.Contains(option.Value) ? "✓" : string.Empty,
                    TextColor = Colors.White,
                    FontSize = 16,
                    FontAttributes = FontAttributes.Bold,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center
                }
            };
            checkBox.StrokeShape = new RoundRectangle { CornerRadius = 1 };

            row.Add(checkBox, 0);
            row.Add(new Label
            {
                Text = option.Name,
                TextColor = Color.FromArgb("#12345A"),
                FontSize = 16,
                VerticalTextAlignment = TextAlignment.Center
            }, 1);
            ProblemPointOptionsLayout.Add(row);
            ProblemPointOptionsLayout.Add(new BoxView { HeightRequest = 1, Color = Color.FromArgb("#EDF1F6"), Margin = new Thickness(4, 0) });
        }

        RefreshProblemPointOptionChecks();
    }

    private void ToggleProblemPoint(QualityDictOption option)
    {
        if (!_pendingProblemValues.Add(option.Value))
        {
            _pendingProblemValues.Remove(option.Value);
        }

        RefreshProblemPointOptionChecks();
    }

    private void RefreshProblemPointOptionChecks()
    {
        foreach (var row in ProblemPointOptionsLayout.Children.OfType<Grid>())
        {
            if (row.BindingContext is not QualityDictOption option || row.Children.OfType<Border>().FirstOrDefault() is not Border checkBox)
            {
                continue;
            }

            var isSelected = _pendingProblemValues.Contains(option.Value);
            checkBox.BackgroundColor = isSelected ? Color.FromArgb("#FF4B55") : Colors.White;
            checkBox.Stroke = isSelected ? Color.FromArgb("#FF4B55") : Color.FromArgb("#8C98AA");
            if (checkBox.Content is Label checkLabel)
            {
                checkLabel.Text = isSelected ? "✓" : string.Empty;
            }
        }
    }

    private void HideProblemPointSheet()
    {
        ProblemPointSheetOverlay.IsVisible = false;
    }

    private void OnProblemPointSheetBackdropTapped(object sender, TappedEventArgs e)
    {
        HideProblemPointSheet();
    }

    private void OnProblemPointSheetCancelTapped(object sender, TappedEventArgs e)
    {
        HideProblemPointSheet();
    }

    private void OnProblemPointSheetCancelClicked(object sender, EventArgs e)
    {
        HideProblemPointSheet();
    }

    private void OnProblemPointSheetConfirmClicked(object sender, EventArgs e)
    {
        foreach (var option in _problemOptions)
        {
            option.IsSelected = _pendingProblemValues.Contains(option.Value);
        }

        RefreshProblemPointDisplay();
        HideProblemPointSheet();
    }

    private void RefreshProblemPointDisplay()
    {
        var selectedNames = _problemOptions
            .Where(item => item.IsSelected)
            .Select(item => item.Name)
            .ToList();

        ProblemPointLabel.Text = selectedNames.Count == 0 ? "点击选择问题点" : string.Join(", ", selectedNames);
        ProblemPointLabel.TextColor = selectedNames.Count == 0 ? Color.FromArgb("#7A889A") : Color.FromArgb("#051B3D");
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (_selectedInspectResult is not QualityDictOption inspectResult)
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
