using JXHLJSApp.Models.Warehouse;
using JXHLJSApp.Models.WorkOrders;
using JXHLJSApp.Services;
using JXHLJSApp.Services.WorkOrders;

namespace JXHLJSApp.Pages.WorkStart;

public partial class ReworkReportPage : ContentPage
{
    private const string ReportModeRework = "rework";

    private readonly IWorkOrderApi _workOrderApi;
    private readonly IScanService _scanService;
    private readonly IProductionContextService _productionContext;
    private List<WorkOrderAbnormalOptionDto> _reworkReasons = new();
    private MaterialQrCodeInfoDto? _material;
    private AttachmentDto? _photo;
    private string? _selectedReworkReason;

    public ReworkReportPage(
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
        if (_reworkReasons.Count == 0)
        {
            await LoadReasonsAsync();
        }
    }

    private async Task LoadReasonsAsync()
    {
        try
        {
            _reworkReasons = await _workOrderApi.GetReworkReasonOptionsAsync();
            _selectedReworkReason = _reworkReasons.FirstOrDefault()?.value;
            RenderReasons();
        }
        catch (Exception ex)
        {
            await DisplayAlert("字典加载失败", ex.Message, "确定");
        }
    }

    private void RenderReasons()
    {
        ReworkReasonContainer.Children.Clear();
        foreach (var option in _reworkReasons)
        {
            var button = new Button
            {
                Text = $"○ {option.name}",
                CommandParameter = option.value,
                Margin = new Thickness(0, 0, 12, 10),
                WidthRequest = 160,
                HeightRequest = 54,
                CornerRadius = 10,
                BackgroundColor = Colors.White,
                TextColor = Color.FromArgb("#001431"),
                BorderColor = Color.FromArgb("#D8E3F3"),
                BorderWidth = 1
            };
            button.Clicked += OnReasonClicked;
            ReworkReasonContainer.Children.Add(button);
        }
        UpdateReasonStyles();
    }

    private void OnReasonClicked(object? sender, EventArgs e)
    {
        if (sender is Button button)
        {
            _selectedReworkReason = button.CommandParameter?.ToString();
            UpdateReasonStyles();
        }
    }

    private void UpdateReasonStyles()
    {
        foreach (var child in ReworkReasonContainer.Children.OfType<Button>())
        {
            var selected = string.Equals(child.CommandParameter?.ToString(), _selectedReworkReason, StringComparison.OrdinalIgnoreCase);
            child.Text = $"{(selected ? "◉" : "○")} {_reworkReasons.FirstOrDefault(o => o.value == child.CommandParameter?.ToString())?.name}";
            child.BackgroundColor = selected ? Color.FromArgb("#EEF6FF") : Colors.White;
            child.BorderColor = selected ? Color.FromArgb("#244B88") : Color.FromArgb("#D8E3F3");
        }
    }

    private async void OnScanTapped(object sender, TappedEventArgs e)
    {
        var code = await _scanService.ScanAsync("扫描返工物料二维码");
        if (string.IsNullOrWhiteSpace(code)) return;
        try
        {
            _material = await _workOrderApi.ScanReworkMaterialAsync(code.Trim());
            BindMaterial();
        }
        catch (Exception ex)
        {
            await DisplayAlert("识别失败", ex.Message, "确定");
        }
    }

    private void BindMaterial()
    {
        SuccessBanner.IsVisible = true;
        FormCard.IsVisible = true;
        SubmitBar.IsVisible = true;
        WorkOrderLabel.Text = ValueOrDash(FirstNonEmpty(_material?.workOrderNo, _productionContext.Current?.WorkOrderNo));
        MaterialCodeLabel.Text = ValueOrDash(_material?.materialCode);
        SteelLabel.Text = ValueOrDash(_material?.steelGrade);
        SpecLabel.Text = ValueOrDash(FirstNonEmpty(_material?.specification, _material?.spec));
        OutputCountLabel.Text = _material?.outputCount is null ? "--" : $"第{_material.outputCount:0.##}件";
    }

    private async void OnPhotoTapped(object sender, TappedEventArgs e)
    {
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
            _photo = await _workOrderApi.UploadReworkAttachmentAsync(photo);
            PhotoHintLabel.Text = "照片已上传";
            PhotoPanel.BackgroundColor = Color.FromArgb("#F2FFF8");
        }
        catch (FeatureNotSupportedException)
        {
            await DisplayAlert("提示", "当前设备不支持调用相机。", "确定");
        }
        catch (Exception ex)
        {
            await DisplayAlert("上传失败", ex.Message, "确定");
        }
    }

    private async void OnSubmitClicked(object sender, EventArgs e)
    {
        if (_material is null) { await DisplayAlert("提示", "请先扫描下料物料二维码。", "确定"); return; }
        if (string.IsNullOrWhiteSpace(_selectedReworkReason)) { await DisplayAlert("提示", "请选择返工原因。", "确定"); return; }
        if (_photo is null) { await DisplayAlert("提示", "请拍摄并上传现场照片。", "确定"); return; }

        var workOrderNo = FirstNonEmpty(_material.workOrderNo, _productionContext.Current?.WorkOrderNo);
        if (string.IsNullOrWhiteSpace(workOrderNo))
        {
            await DisplayAlert("提示", "扫码结果未返回工单号，无法提交返工上报。", "确定");
            return;
        }

        try
        {
            var request = new WorkOrderAbnormalAddRequestDto
            {
                abnormalRecordAttachmentList = new List<AttachmentDto> { _photo },
                materialCode = _material.materialCode,
                materialName = _material.materialName,
                materialType = _material.materialType,
                reportMode = ReportModeRework,
                reworkReason = _selectedReworkReason,
                weight = _material.weight ?? (decimal.TryParse(_material.coilWeight, out var weight) ? weight : null),
                workOrderNo = workOrderNo
            };
            await _workOrderApi.AddAbnormalRecordAsync(request);
            await Shell.Current.GoToAsync(AppShell.RouteAbnormalReportSuccess);
        }
        catch (Exception ex)
        {
            await DisplayAlert("返工提交失败", ex.Message, "确定");
        }
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string ValueOrDash(string? value) => string.IsNullOrWhiteSpace(value) ? "--" : value.Trim();
}
