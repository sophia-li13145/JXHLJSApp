using JXHLJSApp.Services;
using JXHLJSApp.Models.WorkOrders;
using JXHLJSApp.Services.WorkOrders;
using Serilog;

namespace JXHLJSApp.Pages.WorkStart;

public partial class WorkStartOrdersPage : ContentPage, IQueryAttributable
{
    private readonly IWorkOrderApi _workOrderApi;
    private readonly IProductionContextService _productionContext;
    private List<WorkOrderTaskDto> _orders = new();
    private bool _isLoading;
    private bool _refreshRequested = true;
    private bool _isVisible;
    private bool _navigationReady;
    private string? _refreshToken;
    private CancellationTokenSource? _loadCts;
    private string? _boundMachineCode;
    private bool _isStarting;
    private int _viewVersion;
    private CancellationTokenSource _navigationCts = new();

    public WorkStartOrdersPage(
        IWorkOrderApi workOrderApi,
        IProductionContextService productionContext)
    {
        InitializeComponent();
        _workOrderApi = workOrderApi;
        _productionContext = productionContext;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("boundMachineCode", out var value)) return;
        var machineCode = value?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(machineCode)) return;
        var refreshToken = query.TryGetValue("refreshToken", out var token) ? token?.ToString() : null;
        var newBinding = !string.IsNullOrEmpty(refreshToken) && refreshToken != _refreshToken;
        if (!newBinding && string.Equals(machineCode, _boundMachineCode, StringComparison.OrdinalIgnoreCase)) return;
        _boundMachineCode = machineCode;
        _refreshToken = refreshToken;
        _productionContext.Clear();
        InvalidateLoad();
        _refreshRequested = true;
        _orders.Clear();
        OrdersList.ItemsSource = Array.Empty<WorkOrderTaskDto>();
        HeaderLabel.Text = $"正在查询机台 {_boundMachineCode} 的可开工工单...";
        TryRefreshOrders();
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (_navigationCts.IsCancellationRequested)
        {
            _navigationCts.Dispose();
            _navigationCts = new CancellationTokenSource();
        }
        _navigationReady = true;
        if (ErrorDialogService.IsDialogVisible) return;
        _refreshRequested = true;
        TryRefreshOrders();
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        if (Navigation.ModalStack.Any()) return;
        _navigationReady = false;
        InvalidateLoad();
        _refreshRequested = true;
        _navigationCts.Cancel();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _isVisible = true;
        TryRefreshOrders();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _isVisible = false;

        // The error dialog is modal, so showing it does not mean that the user
        // actually left this business page.
        if (!ErrorDialogService.IsDialogVisible)
        {
            _refreshRequested = true;
        }
    }

    private void OnRefreshing(object sender, EventArgs e)
    {
        if (_isStarting)
        {
            RefreshContainer.IsRefreshing = false;
            return;
        }
        InvalidateLoad();
        _refreshRequested = true;
        TryRefreshOrders();
    }

    private void InvalidateLoad()
    {
        _viewVersion++;
        var previous = _loadCts;
        _loadCts = null;
        _isLoading = false;
        previous?.Cancel();
    }

    private void TryRefreshOrders()
    {
        if (!_refreshRequested || !_isVisible || !_navigationReady || _isLoading || _isStarting ||
            _navigationCts.IsCancellationRequested || ErrorDialogService.IsDialogVisible) return;
        _refreshRequested = false;
        _ = LoadOrdersAsync();
    }

    private async Task LoadOrdersAsync()
    {
        if (_isLoading)
        {
            return;
        }

        _isLoading = true;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(_navigationCts.Token);
        _loadCts = request;
        var version = _viewVersion;
        var ct = request.Token;
        var boundMachineCode = _boundMachineCode;

        try
        {
            RefreshContainer.IsRefreshing = true;
            OrdersList.IsEnabled = false;
            _orders.Clear();
            OrdersList.ItemsSource = Array.Empty<WorkOrderTaskDto>();
            Log.Information("Work-order refresh started. Version={Version}, MachineLength={MachineLength}", version, boundMachineCode?.Length ?? 0);
            var orders = await _workOrderApi.GetCurrentUserMachinesWorkOrdersAsync(ct);
            ct.ThrowIfCancellationRequested();
            if (version != _viewVersion || !ReferenceEquals(_loadCts, request) || !_isVisible) return;
            _orders = string.IsNullOrWhiteSpace(boundMachineCode) ? orders : orders
                .Where(order => string.Equals(order.deviceCode?.Trim(), boundMachineCode, StringComparison.OrdinalIgnoreCase))
                .ToList();
            OrdersList.ItemsSource = _orders;
            HeaderLabel.Text = string.IsNullOrWhiteSpace(boundMachineCode)
                ? BuildHeaderText(_orders)
                : $"机台 {boundMachineCode}：{(_orders.Count == 0 ? "暂无可开工工单，请刷新或重新扫码。" : "请选择 1 个工单开工。")}";
            Log.Information("Work-order refresh completed. Version={Version}, Returned={Returned}, Matched={Matched}", version, orders.Count, _orders.Count);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (version != _viewVersion || !ReferenceEquals(_loadCts, request) || !_isVisible) return;
            HeaderLabel.Text = "工单查询失败，请检查网络后下拉刷新重试。";
            await ErrorDialogService.ShowAsync(this, "查询失败", ex.Message, "确定");
        }
        finally
        {
            // A superseded request must not release the newer request's busy state or spinner.
            if (ReferenceEquals(_loadCts, request))
            {
                _loadCts = null;
                RefreshContainer.IsRefreshing = false;
                _isLoading = false;
                OrdersList.IsEnabled = !_isStarting;
                TryRefreshOrders();
            }
        }
    }

    private static string BuildHeaderText(IReadOnlyList<WorkOrderTaskDto> orders)
    {
        if (orders.Count == 0) return "当前绑定机台暂无待执行计划工单。";

        var first = orders[0];
        var machine = string.IsNullOrWhiteSpace(first.deviceCode) ? "--" : first.deviceCode;
        return $"机台 {machine} 待执行计划工单。请选择 1 个工单开工。";
    }

    private async void OnRescanTapped(object sender, TappedEventArgs e)
    {
        if (_isStarting) return;
        if (!string.IsNullOrWhiteSpace(_boundMachineCode))
            await Shell.Current.GoToAsync(AppShell.RouteWorkStartScan);
        else await Shell.Current.GoToAsync("..");
    }

    private async void OnInstructionClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string id } || string.IsNullOrWhiteSpace(id))
        {
            await DisplayAlert("提示", "工单列表主键为空，无法查看生产作业指令卡。", "确定");
            return;
        }

        await Shell.Current.GoToAsync($"{AppShell.RouteWorkOrderInstruction}?id={Uri.EscapeDataString(id)}");
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        if (_isLoading || _isStarting || !ReferenceEquals(Shell.Current.CurrentPage, this)) return;
        if (sender is not Button { CommandParameter: WorkOrderTaskDto order })
        {
            await DisplayAlert("提示", "工单信息为空，无法开工。", "确定");
            return;
        }

        if (string.IsNullOrWhiteSpace(order.workOrderNo))
        {
            await DisplayAlert("提示", "工单号为空，无法开工。", "确定");
            return;
        }

        if (string.IsNullOrWhiteSpace(order.id))
        {
            await DisplayAlert("提示", "工单列表主键为空，无法进入生产执行页面。", "确定");
            return;
        }

        if (!_orders.Contains(order) || (!string.IsNullOrWhiteSpace(_boundMachineCode) &&
            !string.Equals(order.deviceCode?.Trim(), _boundMachineCode, StringComparison.OrdinalIgnoreCase)))
        {
            await DisplayAlert("提示", "工单列表已变化，请刷新后重新选择当前机台的工单。", "确定");
            return;
        }

        var ct = _navigationCts.Token;
        var version = _viewVersion;
        var machineCode = _boundMachineCode ?? order.deviceCode;
        try
        {
            _isStarting = true;
            OrdersList.IsEnabled = false;
            var result = await _workOrderApi.StartWorkOrderAsync(order.workOrderNo, ct);
            ct.ThrowIfCancellationRequested();
            if (version != _viewVersion) return;
            if (!result.success)
            {
                var message = string.IsNullOrWhiteSpace(result.message)
                    ? "接口返回开工失败，请稍后重试。"
                    : result.message;
                await ErrorDialogService.ShowAsync(this, "开工失败", message, "确定");
                return;
            }

            var context = new ProductionContext
            {
                WorkOrderId = order.id,
                WorkOrderNo = order.workOrderNo,
                OperationName = order.operationName,
                MachineCode = machineCode,
                IsPicklingMachineConfirmed = false,
                Status = "Running",
                StartedAt = DateTime.Now
            };

            _productionContext.Set(context);

            await Shell.Current.GoToAsync(AppShell.RouteWorkExecution);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "开工失败", ex.Message, "确定");
        }
        finally
        {
            _isStarting = false;
            OrdersList.IsEnabled = !_isLoading;
            TryRefreshOrders();
        }
    }
}
