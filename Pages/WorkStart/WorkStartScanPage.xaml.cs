using CommunityToolkit.Maui.Core.Platform;
using JXHLJSApp.Services;
using JXHLJSApp.Services.WorkOrders;

namespace JXHLJSApp.Pages.WorkStart;

public partial class WorkStartScanPage : ContentPage, IQueryAttributable
{
    private readonly IWorkOrderApi _workOrderApi;
    private readonly CameraScanService _cameraScanner;
    private bool _isHandlingScan;
    private bool _isBindingMachine;
    private bool _switchMachine;
    private string? _boundMachineCode;
    private bool _isOpeningOrderSelection;
    private bool _isReturning;
    private readonly IProductionContextService _productionContext;
    private CancellationTokenSource _navigationCts = new();

    public WorkStartScanPage(IWorkOrderApi workOrderApi, CameraScanService cameraScanner, IRightKeyScanService scanner, IProductionContextService productionContext)
    {
        InitializeComponent();
        _workOrderApi = workOrderApi;
        _cameraScanner = cameraScanner;
        scanner.Register(this,
            code => ScanAndBindMachineAsync(code),
            () => !_isHandlingScan && !_isBindingMachine && !_isReturning && _boundMachineCode is null);
        _productionContext = productionContext;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _switchMachine = query.TryGetValue("switchMachine", out var value)
            && bool.TryParse(value?.ToString(), out var switching) && switching;
        Title = PageTitleLabel.Text = _switchMachine ? "切换机台" : "扫码上机";
        ScanHintLabel.Text = _switchMachine
            ? $"当前机台 {_productionContext.Current?.MachineCode ?? "未绑定"}\n也可按设备右侧扫描键，验证后重新选择工单"
            : "也可按设备右侧扫描键扫描机台码";
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (!_navigationCts.IsCancellationRequested) return;
        _navigationCts.Dispose();
        _navigationCts = new CancellationTokenSource();
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        // Error dialogs must allow retry; leaving the route cancels a pending binding.
        if (!Navigation.ModalStack.Any()) _navigationCts.Cancel();
    }

    private async void OnBackClicked(object sender, EventArgs e) => await ReturnAsync();

    private async Task ReturnAsync()
    {
        if (_isReturning || _isOpeningOrderSelection) return;
        _isReturning = true;
        try
        {
            // Only canceling an uncommitted machine switch should return to execution.
            var route = _switchMachine && _boundMachineCode is null ? ".." : AppShell.RouteProductionHome;
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "返回失败", ex.Message, "确定");
        }
        finally
        {
            _isReturning = false;
        }
    }

    protected override bool OnBackButtonPressed()
    {
        _ = ReturnAsync();
        return true;
    }

    private async void OnScanClicked(object sender, EventArgs e)
    {
        if (_isReturning) return;
        if (_boundMachineCode is not null) await OpenOrderSelectionAsync();
        else await ScanAndBindMachineAsync();
    }

    private async Task ScanAndBindMachineAsync(string? scanCode = null)
    {
        if (_isHandlingScan || _isBindingMachine || _isReturning || _boundMachineCode is not null) return;

        _isHandlingScan = true;
        try
        {
            scanCode ??= await _cameraScanner.ScanAsync(this, _switchMachine ? "扫描新机台二维码" : "扫码机台二维码上机");
            if (_isReturning || string.IsNullOrWhiteSpace(scanCode)) return;
            await BindMachineAndNavigateAsync(scanCode);
        }
        finally
        {
            _isHandlingScan = false;
        }
    }

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        if (_isHandlingScan || _isReturning) return;
        MachineNoEntry.Unfocus();
        await MachineNoEntry.HideKeyboardAsync(CancellationToken.None);
        await BindMachineAndNavigateAsync(MachineNoEntry.Text);
    }

    private async Task BindMachineAndNavigateAsync(string? machineCode)
    {
        if (_isBindingMachine || _isReturning) return;
        if (_boundMachineCode is not null)
        {
            await OpenOrderSelectionAsync();
            return;
        }
        var devCode = machineCode?.Trim();
        if (string.IsNullOrWhiteSpace(devCode))
        {
            await DisplayAlert("提示", "请输入机台编号", "确定");
            return;
        }

        var ct = _navigationCts.Token;
        try
        {
            _isBindingMachine = true;
            var sessionId = _productionContext.Current?.SessionId;
            if (_switchMachine && sessionId is null)
            {
                await DisplayAlert("提示", "当前生产工单为空，无法切换机台。", "确定");
                return;
            }
            var result = await _workOrderApi.BindWorkerMachineAsync(devCode, ct);
            ct.ThrowIfCancellationRequested();
            if (!result)
            {
                await ErrorDialogService.ShowAsync(this, "绑定失败", "机台绑定未成功，请确认机台编号后重试。", "确定");
                return;
            }

            if (_switchMachine)
            {
                var current = _productionContext.Current;
                if (current is null || current.SessionId != sessionId) return;
            }
            _boundMachineCode = devCode;
            _productionContext.Clear();
            await OpenOrderSelectionAsync();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "绑定失败", ex.Message, "确定");
        }
        finally
        {
            _isBindingMachine = false;
        }
    }

    private async Task OpenOrderSelectionAsync()
    {
        if (_boundMachineCode is null || _isOpeningOrderSelection) return;
        _isOpeningOrderSelection = true;
        try
        {
            // Absolute navigation removes the old execution page and stale selection history.
            await Shell.Current.GoToAsync(AppShell.ProductionOrderSelectionRoute(_boundMachineCode));
        }
        catch (Exception ex)
        {
            ScanButton.Text = "重试进入工单选择";
            ScanHintLabel.Text = "机台已绑定";
            await ErrorDialogService.ShowAsync(this, "进入工单页失败", ex.Message, "确定");
        }
        finally { _isOpeningOrderSelection = false; }
    }
}
