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

        try
        {
            _isBusy = true;
            var result = await _workOrderApi.BindWorkerMachineAsync(code.Trim());
            if (!result)
            {
                await DisplayAlert("识别失败", "机台识别未成功，请重新扫描。", "确定");
                return;
            }

            _machineConfirmed = true;
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
    }

    private void ShowResultStep()
    {
        ScanPanel.IsVisible = false;
        SuccessBanner.IsVisible = true;
        ResultCard.IsVisible = true;
    }

    private void BindMaterial(MaterialQrCodeInfoDto material)
    {
        MaterialCodeEntry.Text = material.materialCode;
        MaterialNameEntry.Text = material.materialName;
        QrCodeEntry.Text = string.IsNullOrWhiteSpace(material.qrCode) ? _lastMaterialQrCode : material.qrCode;
        SpecEntry.Text = material.spec;
        StockBatchEntry.Text = string.IsNullOrWhiteSpace(material.stockBatch) ? material.bizBatchNo : material.stockBatch;
        WorkOrderCodeEntry.Text = _workOrderNo;
    }

    private async void OnConfirmBackClicked(object sender, EventArgs e)
    {
        if (_isBusy) return;

        var input = new MaterialInputConfirmDto
        {
            materialCode = MaterialCodeEntry.Text?.Trim(),
            materialName = MaterialNameEntry.Text?.Trim(),
            qrCode = QrCodeEntry.Text?.Trim(),
            spec = SpecEntry.Text?.Trim(),
            stockBatch = StockBatchEntry.Text?.Trim(),
            workOrderCode = WorkOrderCodeEntry.Text?.Trim()
        };

        if (string.IsNullOrWhiteSpace(input.materialCode))
        {
            await DisplayAlert("提示", "请输入物料编号。", "确定");
            return;
        }

        if (string.IsNullOrWhiteSpace(input.qrCode))
        {
            await DisplayAlert("提示", "请输入二维码。", "确定");
            return;
        }

        if (string.IsNullOrWhiteSpace(input.workOrderCode))
        {
            await DisplayAlert("提示", "请输入工单号。", "确定");
            return;
        }

        try
        {
            _isBusy = true;
            ConfirmButton.IsEnabled = false;
            var result = await _workOrderApi.ConfirmMaterialInputAsync(input);
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
}
