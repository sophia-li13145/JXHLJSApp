using JXHLJSApp.Services;
using JXHLJSApp.Services.WorkOrders;

namespace JXHLJSApp.Pages.WorkStart;

public partial class MaterialUnloadingPage : ContentPage, IQueryAttributable
{
    private readonly IWorkOrderApi _workOrderApi;
    private readonly IScanService _scanService;
    private readonly IProductionContextService _productionContext;
    private bool _machineConfirmed;
    private bool _isBusy;

    public MaterialUnloadingPage(IWorkOrderApi workOrderApi, IScanService scanService, IProductionContextService productionContext)
    {
        InitializeComponent();
        _workOrderApi = workOrderApi;
        _scanService = scanService;
        _productionContext = productionContext;
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnScanPanelTapped(object sender, TappedEventArgs e)
    {
        if (_isBusy) return;
        if (!_machineConfirmed) await ScanMachineAsync();
        else await ScanMaterialAsync();
    }

    private async Task ScanMachineAsync()
    {
        var devCode = await _scanService.ScanAsync("扫描机台二维码");
        var workOrderNo = _productionContext.Current?.WorkOrderNo;
        if (string.IsNullOrWhiteSpace(devCode) || string.IsNullOrWhiteSpace(workOrderNo)) return;

        try
        {
            _isBusy = true;
            if (!await _workOrderApi.ScanToWorkAsync(devCode.Trim(), workOrderNo))
            {
                await ErrorDialogService.ShowAsync(this, "识别失败", "机台识别未成功，请确认机台码后重试。", "确定");
                return;
            }
            _machineConfirmed = true;
            UpdateProductionContextMachine(devCode.Trim());
            ScanIconLabel.Text = "📦";
            ScanTitleLabel.Text = "2. 机台识别确认";
            ScanHintLabel.Text = "点击扫描下料标签二维码";
        }
        catch (Exception ex) { await ErrorDialogService.ShowAsync(this, "识别失败", ex.Message, "确定"); }
        finally { _isBusy = false; }
    }

    private void UpdateProductionContextMachine(string machineCode)
    {
        var current = _productionContext.Current;
        if (current is null) return;

        _productionContext.Set(new ProductionContext
        {
            WorkOrderId = current.WorkOrderId,
            WorkOrderNo = current.WorkOrderNo,
            OperationName = current.OperationName,
            ExecutionId = current.ExecutionId,
            MachineCode = machineCode,
            Status = current.Status,
            StartedAt = current.StartedAt,
            SessionId = current.SessionId
        });
    }

    private async Task ScanMaterialAsync()
    {
        var qrCode = await _scanService.ScanAsync("扫描下料标签二维码");
        var workOrderNo = _productionContext.Current?.WorkOrderNo;
        if (string.IsNullOrWhiteSpace(qrCode) || string.IsNullOrWhiteSpace(workOrderNo)) return;

        await Shell.Current.GoToAsync($"{AppShell.RouteMaterialUnloadingDetail}?qrCode={Uri.EscapeDataString(qrCode.Trim())}&workOrderNo={Uri.EscapeDataString(workOrderNo)}");
    }
}
