using CommunityToolkit.Maui.Core.Platform;
using JXHLJSApp.Services;
using JXHLJSApp.Services.WorkOrders;

namespace JXHLJSApp.Pages.WorkStart;

public partial class WorkStartScanPage : ContentPage, IQueryAttributable
{
    private readonly IWorkOrderApi _workOrderApi;
    private readonly IRightKeyScanService _scanner;
    private bool _isHandlingScan;
    private bool _isBindingMachine;
    private bool _switchMachine;
    private string? _boundMachineCode;
    private bool _isOpeningOrderSelection;
    private readonly IProductionContextService _productionContext;
    private CancellationTokenSource _navigationCts = new();

    public WorkStartScanPage(IWorkOrderApi workOrderApi, IRightKeyScanService scanner, IProductionContextService productionContext)
    {
        InitializeComponent();
        _workOrderApi = workOrderApi;
        _scanner = scanner;
        _productionContext = productionContext;
        _scanner.Register(this,
            code => ScanAndBindMachineAsync(code),
            () => !_isHandlingScan && !_isBindingMachine && _boundMachineCode is null);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _switchMachine = query.TryGetValue("switchMachine", out var value)
            && bool.TryParse(value?.ToString(), out var switching) && switching;
        Title = PageTitleLabel.Text = _switchMachine ? "切换机台" : "扫码上机";
        ScanHintLabel.Text = _switchMachine
            ? "扫描新机台，成功后重新选择工单开工"
            : "对准机台标签，无需点击屏幕";
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

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        if (_boundMachineCode is not null) await OpenOrderSelectionAsync();
        else await Shell.Current.GoToAsync("..");
    }

    protected override bool OnBackButtonPressed()
    {
        if (_boundMachineCode is null) return base.OnBackButtonPressed();
        _ = OpenOrderSelectionAsync();
        return true;
    }

    private async void OnScanTapped(object sender, TappedEventArgs e)
    {
        if (_boundMachineCode is not null) await OpenOrderSelectionAsync();
        else await ScanAndBindMachineAsync();
    }

    private async Task ScanAndBindMachineAsync(string? scanCode = null)
    {
        if (string.IsNullOrWhiteSpace(scanCode))
        {
            await _scanner.ShowHintAsync(this, _switchMachine ? "扫描新机台二维码" : "扫码机台二维码上机");
            return;
        }

        if (_isHandlingScan) return;
        _isHandlingScan = true;
        try
        {
            var code = scanCode;
            if (!string.IsNullOrWhiteSpace(code))
            {
                await BindMachineAndNavigateAsync(code);
            }
        }
        finally
        {
            _isHandlingScan = false;
        }
    }

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        MachineNoEntry.Unfocus();
        await MachineNoEntry.HideKeyboardAsync(CancellationToken.None);
        await BindMachineAndNavigateAsync(MachineNoEntry.Text);
    }

    private async Task BindMachineAndNavigateAsync(string? machineCode)
    {
        if (_isBindingMachine) return;
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
            ScanHintLabel.Text = "机台已绑定，点击此处重试进入工单选择页";
            await ErrorDialogService.ShowAsync(this, "进入工单页失败", ex.Message, "确定");
        }
        finally { _isOpeningOrderSelection = false; }
    }
}
