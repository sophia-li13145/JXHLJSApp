using JXHLJSApp.Models.Warehouse;
using JXHLJSApp.Services;
using JXHLJSApp.Services.Warehouse;
using System.Collections.ObjectModel;

namespace JXHLJSApp.Pages.Warehouse;

[QueryProperty(nameof(DeliveryNo), "deliveryNo")]
public partial class DeliveryOrderDetailPage : ContentPage
{
    private readonly IWarehouseApi _warehouseApi;
    private readonly IScanService _scanService;
    private readonly ObservableCollection<DeliveryOrderMaterialDetailDto> _materials = new();
    private string? _deliveryNo;
    private DeliveryOrderDetailDto? _detail;

    public string? DeliveryNo
    {
        get => _deliveryNo;
        set => _deliveryNo = Uri.UnescapeDataString(value ?? string.Empty);
    }

    public DeliveryOrderDetailPage(IWarehouseApi warehouseApi, IScanService scanService)
    {
        InitializeComponent();
        _warehouseApi = warehouseApi;
        _scanService = scanService;
        MaterialList.ItemsSource = _materials;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDetailAsync();
    }

    private async Task LoadDetailAsync()
    {
        if (string.IsNullOrWhiteSpace(_deliveryNo))
        {
            await DisplayAlert("提示", "发货单号为空，无法查询详情。", "确定");
            return;
        }

        try
        {
            _detail = await _warehouseApi.GetDeliveryOrderDetailAsync(_deliveryNo);
            BindDetail(_detail);
        }
        catch (Exception ex)
        {
            await DisplayAlert("加载失败", ex.Message, "确定");
        }
    }

    private void BindDetail(DeliveryOrderDetailDto detail)
    {
        AuditStatusLabel.Text = detail.auditStatusDisplay;
        DeliveryNoLabel.Text = detail.deliveryNoDisplay;
        CustomerLabel.Text = detail.customerDisplay;
        AddressLabel.Text = detail.consAddressDisplay;
        ContactLabel.Text = "--";
        DeliveryDateLabel.Text = detail.expectedDeliveryDateDisplay;
        CarrierLabel.Text = detail.carrierNameDisplay;
        CarrierLicenseLabel.Text = detail.carrierLicenseDisplay;
        LogisticsNumberLabel.Text = detail.logisticsNumberDisplay;

        _materials.Clear();
        foreach (var item in detail.detailList ?? new List<DeliveryOrderMaterialDetailDto>())
        {
            _materials.Add(item);
        }

        RefreshScanProgress();
    }

    private async void OnScanMaterialClicked(object sender, EventArgs e)
    {
        var code = await _scanService.ScanAsync("扫描物料码");
        if (string.IsNullOrWhiteSpace(code)) return;

        await ScanActualAsync(code.Trim());
    }

    private async Task ScanActualAsync(string barcode)
    {
        if (string.IsNullOrWhiteSpace(_deliveryNo))
        {
            await DisplayAlert("提示", "发货单号为空，无法扫码确认。", "确定");
            return;
        }

        try
        {
            var result = await _warehouseApi.ScanDeliveryActualAsync(new DeliveryOrderScanActualRequestDto
            {
                barcode = barcode,
                deliveryNo = _deliveryNo
            });

            ApplyScanActualResult(result);
            await LoadDetailAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("扫码确认失败", ex.Message, "确定");
        }
    }

    private void ApplyScanActualResult(DeliveryOrderScanActualResultDto result)
    {
        var item = _materials.FirstOrDefault(material =>
            string.Equals(material.materialCode, result.materialCode, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(material.stockBatch, result.stockBatch, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(material.id, result.scanDetailId, StringComparison.OrdinalIgnoreCase));

        if (item is null)
        {
            return;
        }

        item.scannedQty = result.scannedQty ?? result.actualQty ?? item.scannedQty;
        MaterialList.ItemsSource = null;
        MaterialList.ItemsSource = _materials;
        RefreshScanProgress();
    }

    private void RefreshScanProgress()
    {
        var total = _materials.Count;
        var scanned = _materials.Count(item => item.isScanned);
        ScanProgressLabel.Text = $"{scanned} / {total} 件";
    }

    private async void OnConfirmDeliveryClicked(object sender, EventArgs e)
    {
        if (_materials.Count == 0)
        {
            await DisplayAlert("提示", "暂无可确认的发货明细。", "确定");
            return;
        }

        if (_materials.Any(item => !item.isScanned))
        {
            await DisplayAlert("提示", "请先完成全部物料扫码复核。", "确定");
            return;
        }

        if (string.IsNullOrWhiteSpace(_deliveryNo))
        {
            await DisplayAlert("提示", "发货单号为空，无法确认发货完成。", "确定");
            return;
        }

        try
        {
            var success = await _warehouseApi.ConfirmDeliveryCompletionAsync(_deliveryNo);
            if (success is false)
            {
                await DisplayAlert("确认失败", "接口返回确认失败，请稍后重试。", "确定");
                return;
            }

            await Shell.Current.GoToAsync(AppShell.RouteDeliveryCompletionSuccess);
        }
        catch (Exception ex)
        {
            await DisplayAlert("确认失败", ex.Message, "确定");
        }
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
