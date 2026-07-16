using JXHLJSApp.Models.Warehouse;
using JXHLJSApp.Models.WorkOrders;
using JXHLJSApp.Services;
using JXHLJSApp.Services.WorkOrders;

namespace JXHLJSApp.Pages.WorkStart;

public partial class AbnormalReportPage : ContentPage
{
    private const string ReportModeAbnormal = "abnormal";

    private readonly IWorkOrderApi _workOrderApi;
    private readonly IScanService _scanService;
    private readonly IProductionContextService _productionContext;
    private List<WorkOrderAbnormalOptionDto> _options = new();
    private MaterialQrCodeInfoDto? _material;
    private string? _scannedQrCode;
    private AttachmentDto? _photo;
    private byte[]? _photoPreviewBytes;
    private string? _selectedAbnormalType;

    public AbnormalReportPage(
        IWorkOrderApi workOrderApi,
        IScanService scanService,
        IProductionContextService productionContext)
    {
        InitializeComponent();
        _workOrderApi = workOrderApi;
        _scanService = scanService;
        _productionContext = productionContext;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_options.Count == 0)
        {
            await LoadOptionsAsync();
        }
    }

    private async Task LoadOptionsAsync()
    {
        try
        {
            _options = await _workOrderApi.GetAbnormalTypeOptionsAsync();
            _selectedAbnormalType = _options.FirstOrDefault()?.value;
            RenderOptions();
        }
        catch (Exception ex)
        {
            await DisplayAlert("字典加载失败", ex.Message, "确定");
        }
    }

    private void RenderOptions()
    {
        AbnormalTypeContainer.Children.Clear();
        AbnormalTypeContainer.RowDefinitions.Clear();

        for (var index = 0; index < _options.Count; index++)
        {
            if (index % 2 == 0)
            {
                AbnormalTypeContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            var option = _options[index];
            var button = new Button
            {
                Text = $"○ {option.name}",
                CommandParameter = option.value,
                HeightRequest = 54,
                CornerRadius = 10,
                BackgroundColor = Colors.White,
                TextColor = Color.FromArgb("#244B88"),
                BorderColor = Color.FromArgb("#D8E3F3"),
                BorderWidth = 1,
                HorizontalOptions = LayoutOptions.Fill
            };
            button.Clicked += OnAbnormalTypeClicked;
            AbnormalTypeContainer.Add(button, index % 2, index / 2);
        }
        UpdateOptionStyles();
    }

    private void OnAbnormalTypeClicked(object? sender, EventArgs e)
    {
        if (sender is Button button)
        {
            _selectedAbnormalType = button.CommandParameter?.ToString();
            UpdateOptionStyles();
        }
    }

    private void UpdateOptionStyles()
    {
        foreach (var child in AbnormalTypeContainer.Children.OfType<Button>())
        {
            var selected = string.Equals(child.CommandParameter?.ToString(), _selectedAbnormalType, StringComparison.OrdinalIgnoreCase);
            child.Text = $"{(selected ? "◉" : "○")} {_options.FirstOrDefault(o => o.value == child.CommandParameter?.ToString())?.name}";
            child.BackgroundColor = selected ? Color.FromArgb("#EEF6FF") : Colors.White;
            child.BorderColor = selected ? Color.FromArgb("#244B88") : Color.FromArgb("#D8E3F3");
        }
    }

    private async void OnScanTapped(object sender, TappedEventArgs e)
    {
        var code = await _scanService.ScanAsync("扫描异常对象二维码");
        if (string.IsNullOrWhiteSpace(code)) return;
        try
        {
            _scannedQrCode = code.Trim();
            _material = await _workOrderApi.ScanAbnormalMaterialAsync(_scannedQrCode);
            BindMaterial();
        }
        catch (Exception ex)
        {
            await DisplayAlert("识别失败", ex.Message, "确定");
        }
    }

    private void BindMaterial()
    {
        MaterialCard.IsVisible = true;
        MaterialTypeLabel.Text = $"✅ 识别成功: {ValueOrDash(_material?.materialType)}";
        MaterialCodeLabel.Text = ValueOrDash(_material?.materialCode);
        SteelLabel.Text = ValueOrDash(_material?.steelGrade);
        OriginLabel.Text = ValueOrDash(_material?.originPlace);
        SpecLabel.Text = ValueOrDash(FirstNonEmpty(_material?.specification, _material?.spec));
        WeightLabel.Text = FormatWeight(_material);
        WorkOrderLabel.Text = ValueOrDash(FirstNonEmpty(_material?.workOrderNo, _productionContext.Current?.WorkOrderNo));
    }

    private async void OnPhotoTapped(object sender, TappedEventArgs e) => await CaptureAndUploadPhotoAsync();

    private async void OnRetakePhotoClicked(object sender, EventArgs e) => await CaptureAndUploadPhotoAsync();

    private async Task LoadPhotoPreviewAsync(FileResult photo)
    {
        await using var stream = await photo.OpenReadAsync();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        _photoPreviewBytes = memory.ToArray();
    }

    private void SetPhotoLoading(bool isLoading)
    {
        PhotoLoadingOverlay.IsVisible = isLoading;
        PhotoLoadingIndicator.IsRunning = isLoading;
        RetakePhotoButton.IsEnabled = !isLoading;
    }

    private void ShowPhotoPreview()
    {
        if (_photoPreviewBytes is null || _photoPreviewBytes.Length == 0)
        {
            return;
        }

        PhotoPreviewImage.Source = ImageSource.FromStream(() => new MemoryStream(_photoPreviewBytes));
        PhotoPanel.HeightRequest = 340;
        PhotoPreviewImage.IsVisible = true;
        PhotoPlaceholder.IsVisible = false;
        RetakePhotoButton.IsVisible = true;
        PhotoPanel.BackgroundColor = Colors.White;
        PhotoPanel.StrokeDashArray = null;
    }

    private async Task CaptureAndUploadPhotoAsync()
    {
        if (_material is null)
        {
            await DisplayAlert("提示", "请先扫描识别异常对象。", "确定");
            return;
        }

        try
        {
            var permission = await Permissions.RequestAsync<Permissions.Camera>();
            if (permission != PermissionStatus.Granted)
            {
                await DisplayAlert("提示", "未获得相机权限，无法拍照。", "确定");
                return;
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions { Title = "现场拍照" });
            if (photo is null) return;

            SetPhotoLoading(true);
            try
            {
                await LoadPhotoPreviewAsync(photo);
                _photo = await _workOrderApi.UploadAbnormalAttachmentAsync(photo);
                ShowPhotoPreview();
            }
            finally
            {
                SetPhotoLoading(false);
            }
        }
        catch (Exception ex) when (ex is FeatureNotSupportedException or FeatureNotEnabledException)
        {
            await DisplayAlert("拍照失败", "当前设备不支持调用相机或相机功能未启用。", "确定");
        }
        catch (Exception ex) when (ex is PermissionException or UnauthorizedAccessException)
        {
            await DisplayAlert("权限错误", "没有相机或照片文件访问权限，请在系统设置中授权后重试。", "确定");
        }
        catch (HttpRequestException ex)
        {
            await DisplayAlert("接口错误", $"照片上传接口请求失败：{ex.Message}", "确定");
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlert("接口错误", $"照片上传接口返回异常：{ex.Message}", "确定");
        }
        catch (IOException ex)
        {
            await DisplayAlert("照片读取失败", $"已拍摄照片读取失败：{ex.Message}", "确定");
        }
        catch (Exception ex)
        {
            await DisplayAlert("拍照或上传失败", ex.Message, "确定");
        }
    }

    private async void OnSubmitClicked(object sender, EventArgs e)
    {
        if (_material is null) { await DisplayAlert("提示", "请先扫描异常对象二维码。", "确定"); return; }
        if (string.IsNullOrWhiteSpace(_selectedAbnormalType)) { await DisplayAlert("提示", "请选择异常类型。", "确定"); return; }
        if (_photo is null) { await DisplayAlert("提示", "请拍摄并上传现场照片。", "确定"); return; }

        var workOrderNo = FirstNonEmpty(_material.workOrderNo, _productionContext.Current?.WorkOrderNo);
        if (string.IsNullOrWhiteSpace(workOrderNo))
        {
            await DisplayAlert("提示", "扫码结果未返回工单号，无法提交异常上报。", "确定");
            return;
        }

        try
        {
            var request = new WorkOrderAbnormalAddRequestDto
            {
                abnormalRecordAttachmentList = new List<AttachmentDto> { _photo },
                abnormalType = _selectedAbnormalType,
                materialCode = _material.materialCode,
                materialName = _material.materialName,
                materialType = _material.materialType,
                qrCode = _scannedQrCode,
                reportMode = ReportModeAbnormal,
                supplementaryDescription = DescriptionEditor.Text,
                weight = _material.weight ?? (decimal.TryParse(_material.coilWeight, out var weight) ? weight : null),
                workOrderNo = workOrderNo
            };
            await _workOrderApi.AddAbnormalRecordAsync(request);
            await Shell.Current.GoToAsync(AppShell.RouteAbnormalReportSuccess);
        }
        catch (Exception ex)
        {
            await DisplayAlert("上报失败", ex.Message, "确定");
        }
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private static string FormatWeight(MaterialQrCodeInfoDto? material)
    {
        var value = material?.weight?.ToString("0.##") ?? material?.coilWeight;
        return string.IsNullOrWhiteSpace(value) ? "--" : $"{value} {ValueOrDash(FirstNonEmpty(material?.unit, material?.weightUnit))}";
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string ValueOrDash(string? value) => string.IsNullOrWhiteSpace(value) ? "--" : value.Trim();
}
