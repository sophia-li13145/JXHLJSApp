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
    private bool _isRedirectingToDetail;

    public MaterialUnloadingPage(IWorkOrderApi workOrderApi, IScanService scanService, IProductionContextService productionContext)
    {
        InitializeComponent();
        _workOrderApi = workOrderApi;
        _scanService = scanService;
        _productionContext = productionContext;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        // 兼容旧的手动下料链接：扫码路由收到手动下料参数时直接转到详情路由。
        // 不带 inputRecordId 的正常扫码下料仍完整保留原有机台、物料扫码流程。
        if (_isRedirectingToDetail || !TryReadQueryValue(query, "inputRecordId", out var inputRecordId))
        {
            return;
        }

        _isRedirectingToDetail = true;
        var detailParameters = new Dictionary<string, object>
        {
            ["inputRecordId"] = inputRecordId,
            ["qrCode"] = ReadQueryValue(query, "qrCode"),
            ["workOrderNo"] = ReadQueryValue(query, "workOrderNo")
        };

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await Shell.Current.GoToAsync(AppShell.RouteMaterialUnloadingDetail, detailParameters);
            }
            catch (Exception ex)
            {
                await ErrorDialogService.ShowAsync(this, "跳转失败", ex.Message, "确定");
            }
            finally
            {
                _isRedirectingToDetail = false;
            }
        });
    }

    private static bool TryReadQueryValue(IDictionary<string, object> query, string key, out string value)
    {
        value = ReadQueryValue(query, key);
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string ReadQueryValue(IDictionary<string, object> query, string key) =>
        query.TryGetValue(key, out var value)
            ? Uri.UnescapeDataString(value?.ToString() ?? string.Empty)
            : string.Empty;

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
