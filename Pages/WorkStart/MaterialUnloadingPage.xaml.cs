using JXHLJSApp.Services;
using JXHLJSApp.Services.WorkOrders;

namespace JXHLJSApp.Pages.WorkStart;

public partial class MaterialUnloadingPage : ContentPage, IQueryAttributable
{
    private readonly IWorkOrderApi _workOrderApi;
    private readonly IRightKeyScanService _scanner;
    private bool _isHandlingScan;
    private readonly IProductionContextService _productionContext;
    private bool _machineConfirmed;
    private bool _isBusy;
    private bool _isRedirectingToDetail;

    public MaterialUnloadingPage(IWorkOrderApi workOrderApi, IRightKeyScanService scanner, IProductionContextService productionContext)
    {
        InitializeComponent();
        _workOrderApi = workOrderApi;
        _scanner = scanner;
        _scanner.Register(this,
            code => _machineConfirmed ? ScanMaterialAsync(code) : ScanMachineAsync(code),
            () => !_isHandlingScan && (!_isBusy && !_isRedirectingToDetail));
        _productionContext = productionContext;
        ReuseConfirmedPicklingMachine();
    }

    private void ReuseConfirmedPicklingMachine()
    {
        var current = _productionContext.Current;
        if (current?.IsPicklingMachineConfirmed == true &&
            IsPicklingProcess(current.OperationName) &&
            !string.IsNullOrWhiteSpace(current.MachineCode))
        {
            _machineConfirmed = true;
            ShowMaterialScanStep();
        }
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

    private async Task ScanMachineAsync(string? scanCode = null)
    {
        if (string.IsNullOrWhiteSpace(scanCode))
        {
            await _scanner.ShowHintAsync(this, "扫描机台二维码");
            return;
        }

        if (_isHandlingScan) return;
        _isHandlingScan = true;
        try
        {
            var devCode = scanCode;
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
                ShowMaterialScanStep();
            }
            catch (Exception ex) { await ErrorDialogService.ShowAsync(this, "识别失败", ex.Message, "确定"); }
            finally { _isBusy = false; }
        }
        finally
        {
            _isHandlingScan = false;
        }
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
            IsPicklingMachineConfirmed = current.IsPicklingMachineConfirmed || IsPicklingProcess(current.OperationName),
            Status = current.Status,
            StartedAt = current.StartedAt,
            SessionId = current.SessionId
        });
    }

    private void ShowMaterialScanStep()
    {
        ScanIconLabel.Text = "📦";
        ScanTitleLabel.Text = "2. 扫描物料二维码";
        ScanHintLabel.Text = "按右侧扫描键扫描下料标签二维码";
    }

    private static bool IsPicklingProcess(string? operationName) =>
        operationName?.Contains("酸洗", StringComparison.OrdinalIgnoreCase) == true;

    private async Task ScanMaterialAsync(string? scanCode = null)
    {
        if (string.IsNullOrWhiteSpace(scanCode))
        {
            await _scanner.ShowHintAsync(this, "扫描下料标签二维码");
            return;
        }

        if (_isHandlingScan) return;
        _isHandlingScan = true;
        try
        {
            var qrCode = scanCode;
            var workOrderNo = _productionContext.Current?.WorkOrderNo;
            if (string.IsNullOrWhiteSpace(qrCode) || string.IsNullOrWhiteSpace(workOrderNo)) return;

            // 先查询成功再进入详情；失败时停留在当前物料扫码步骤。
            var materialQrCode = qrCode.Trim();
            var inputOutput = (await _workOrderApi.GetWorkOrderInputOutputAsync(workOrderNo, materialQrCode)).FirstOrDefault();
            if (inputOutput is null)
            {
                throw new InvalidOperationException("未查询到该物料对应的下料详情，请重新扫描物料二维码。");
            }

            await Shell.Current.GoToAsync(AppShell.RouteMaterialUnloadingDetail, new Dictionary<string, object>
            {
                ["qrCode"] = materialQrCode,
                ["workOrderNo"] = workOrderNo,
                ["inputOutput"] = inputOutput
            });
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "扫码失败", ex.Message, "确定");
        }
        finally
        {
            _isHandlingScan = false;
        }
    }
}
