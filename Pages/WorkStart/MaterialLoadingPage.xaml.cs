using JXHLJSApp.Models.WorkOrders;
using JXHLJSApp.Services;
using JXHLJSApp.Services.WorkOrders;

namespace JXHLJSApp.Pages.WorkStart;

[QueryProperty(nameof(WorkOrderId), "id")]
[QueryProperty(nameof(WorkOrderNo), "workOrderNo")]
public partial class MaterialLoadingPage : ContentPage
{
    private readonly IWorkOrderApi _workOrderApi;
    private readonly IScanService _scanService;
    private string? _workOrderId;
    private string? _workOrderNo;
    private bool _machineConfirmed;
    private bool _isBusy;
    private string? _lastMaterialQrCode;
    private MaterialInputConfirmDto? _confirmInput;

    public string? WorkOrderId
    {
        get => _workOrderId;
        set => _workOrderId = Uri.UnescapeDataString(value ?? string.Empty);
    }

    public string? WorkOrderNo
    {
        get => _workOrderNo;
        set => _workOrderNo = Uri.UnescapeDataString(value ?? string.Empty);
    }

    public MaterialLoadingPage(IWorkOrderApi workOrderApi, IScanService scanService)
    {
        InitializeComponent();
        _workOrderApi = workOrderApi;
        _scanService = scanService;
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnScanPanelTapped(object sender, TappedEventArgs e)
    {
        if (_isBusy) return;

        if (!_machineConfirmed)
        {
            await ScanMachineAsync();
            return;
        }

        await ScanMaterialAsync();
    }

    private async Task ScanMachineAsync()
    {
        var code = await _scanService.ScanAsync("扫描机台二维码");
        if (string.IsNullOrWhiteSpace(code)) return;

        await ConfirmMachineAsync(code);
    }

    private async void OnManualMachineConfirmClicked(object sender, EventArgs e)
    {
        if (_isBusy) return;

        await ConfirmMachineAsync(MachineCodeEntry.Text);
    }

    private async void OnManualMachineCompleted(object sender, EventArgs e)
    {
        if (_isBusy) return;

        await ConfirmMachineAsync(MachineCodeEntry.Text);
    }

    private async Task ConfirmMachineAsync(string? machineCode)
    {
        var devCode = machineCode?.Trim();
        if (string.IsNullOrWhiteSpace(devCode))
        {
            await DisplayAlert("提示", "请输入机台码", "确定");
            return;
        }

        if (string.IsNullOrWhiteSpace(_workOrderNo))
        {
            await DisplayAlert("提示", "工单号为空，无法扫码开工。", "确定");
            return;
        }

        try
        {
            _isBusy = true;
            var result = await _workOrderApi.ScanToWorkAsync(devCode, _workOrderNo);
            if (!result)
            {
                await DisplayAlert("识别失败", "机台识别未成功，请确认机台码后重试。", "确定");
                return;
            }

            _machineConfirmed = true;
            MachineCodeEntry.Text = devCode;
            ShowMaterialScanStep();
        }
        catch (Exception ex)
        {
            await DisplayAlert("识别失败", ex.Message, "确定");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task ScanMaterialAsync()
    {
        var code = await _scanService.ScanAsync("扫描上料物料二维码");
        if (string.IsNullOrWhiteSpace(code)) return;

        try
        {
            _isBusy = true;
            _lastMaterialQrCode = code.Trim();
            var material = await _workOrderApi.ScanQueryMaterialInfoAsync(_lastMaterialQrCode);
            BindMaterial(material);
            ShowResultStep();
        }
        catch (Exception ex)
        {
            await DisplayAlert("扫码失败", ex.Message, "确定");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void ShowMaterialScanStep()
    {
        ScanIconLabel.Text = "📦";
        ScanTitleLabel.Text = "2. 机台识别确认";
        ScanHintLabel.Text = "点击扫描上料物料二维码";
        ManualMachinePanel.IsVisible = false;
    }

    private void ShowResultStep()
    {
        ScanPanel.IsVisible = false;
        SuccessBanner.IsVisible = true;
        ResultCard.IsVisible = true;
    }

    private void BindMaterial(MaterialQrCodeInfoDto material)
    {
        _confirmInput = new MaterialInputConfirmDto
        {
            materialCode = material.materialCode,
            materialName = material.materialName,
            qrCode = string.IsNullOrWhiteSpace(material.qrCode) ? _lastMaterialQrCode : material.qrCode,
            spec = material.spec,
            stockBatch = string.IsNullOrWhiteSpace(material.stockBatch) ? material.bizBatchNo : material.stockBatch,
            workOrderCode = _workOrderNo
        };

        MaterialCodeLabel.Text = ValueOrDash(_confirmInput.materialCode);
        MaterialNameLabel.Text = ValueOrDash(_confirmInput.materialName);
        QrCodeLabel.Text = ValueOrDash(_confirmInput.qrCode);
        SpecLabel.Text = ValueOrDash(_confirmInput.spec);
        StockBatchLabel.Text = ValueOrDash(_confirmInput.stockBatch);
        WorkOrderCodeLabel.Text = ValueOrDash(_confirmInput.workOrderCode);
    }

    private async void OnConfirmBackClicked(object sender, EventArgs e)
    {
        if (_isBusy) return;

        if (_confirmInput is null)
        {
            await DisplayAlert("提示", "请先扫描上料物料二维码。", "确定");
            return;
        }

        if (string.IsNullOrWhiteSpace(_confirmInput.materialCode))
        {
            await DisplayAlert("提示", "物料编号为空，无法确认上料。", "确定");
            return;
        }

        if (string.IsNullOrWhiteSpace(_confirmInput.qrCode))
        {
            await DisplayAlert("提示", "二维码为空，无法确认上料。", "确定");
            return;
        }

        if (string.IsNullOrWhiteSpace(_confirmInput.workOrderCode))
        {
            await DisplayAlert("提示", "工单号为空，无法确认上料。", "确定");
            return;
        }

        try
        {
            _isBusy = true;
            ConfirmButton.IsEnabled = false;
            var result = await _workOrderApi.ConfirmMaterialInputAsync(_confirmInput);
            if (!result)
            {
                await DisplayAlert("确认失败", "接口返回上料确认失败，请稍后重试。", "确定");
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await DisplayAlert("确认失败", ex.Message, "确定");
        }
        finally
        {
            _isBusy = false;
            ConfirmButton.IsEnabled = true;
        }
    }

    private static string ValueOrDash(string? value) => string.IsNullOrWhiteSpace(value) ? "--" : value;
}
