using JXHLJSApp.Models.WorkOrders;
using JXHLJSApp.Services;
using JXHLJSApp.Services.WorkOrders;

namespace JXHLJSApp.Pages.WorkStart;

public partial class WorkExecutionPage : ContentPage, IQueryAttributable
{
    private readonly IWorkOrderApi _workOrderApi;
    private bool _isOpeningMachineScanner;
    private readonly IProductionContextService _productionContext;
    private List<WorkOrderDetailDto> _tasks = new();
    private bool _isLoadingTaskPool;
    public WorkExecutionPage(
        IWorkOrderApi workOrderApi,
        IProductionContextService productionContext)
    {
        InitializeComponent();
        _workOrderApi = workOrderApi;
        _productionContext = productionContext;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("pauseUpdated", out var value) ||
            !string.Equals(value?.ToString(), "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // 从“暂停作业”返回时立即重新拉取任务池与暂停记录，避免数据滞后。
        _ = MainThread.InvokeOnMainThreadAsync(LoadTaskPoolAsync);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Closing a modal error dialog reactivates the underlying page on Android.
        // Do not treat that lifecycle callback as a new backend operation.
        if (ErrorDialogService.IsDialogVisible)
        {
            return;
        }
        await LoadTaskPoolAsync();
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        await LoadTaskPoolAsync();
    }

    private async Task LoadTaskPoolAsync()
    {
        // 返回导航会同时触发 ApplyQueryAttributes 与 OnAppearing，这里做并发保护，只发一次请求。
        if (_isLoadingTaskPool)
        {
            return;
        }

        _isLoadingTaskPool = true;
        try
        {
            var current = _productionContext.Current;
            var workOrderNo = current?.WorkOrderNo;
            var isPickling = current?.OperationName?.Contains("酸洗", StringComparison.OrdinalIgnoreCase) == true;
            InstructionCardButton.IsVisible = !isPickling;
            WorkCompletionButton.IsVisible = !isPickling;
            PauseButton.IsVisible = !isPickling;
            SwitchMachineButton.IsVisible = !isPickling;
            ManualUnloadingCardButton.IsVisible = isPickling;
            if (string.IsNullOrWhiteSpace(workOrderNo))
            {
                await DisplayAlert("提示", "当前生产工单为空，无法查询当前关联任务池。", "确定");
                TaskPoolList.ItemsSource = Array.Empty<WorkOrderDetailDto>();
                return;
            }

            RefreshContainer.IsRefreshing = true;
            _tasks = await _workOrderApi.GetCurrentTaskPoolAsync(workOrderNo);
            foreach (var task in _tasks)
            {
                task.isPicklingContext = isPickling;
            }

            // 停工产出长度记录挂在任务卡“生产重量”下方，查询失败不影响任务池展示。
            await ApplyStopLengthRecordsAsync(_tasks, workOrderNo!);

            TaskPoolList.ItemsSource = _tasks;
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "查询失败", ex.Message, "确定");
        }
        finally
        {
            _isLoadingTaskPool = false;
            RefreshContainer.IsRefreshing = false;
        }
    }

    private async Task ApplyStopLengthRecordsAsync(List<WorkOrderDetailDto> tasks, string workOrderNo)
    {
        if (tasks.Count == 0)
        {
            return;
        }

        try
        {
            var records = await _workOrderApi.GetWorkOrderStopLengthListAsync(workOrderNo);
            if (records.Count == 0)
            {
                return;
            }

            var matched = tasks
                .Where(task => string.Equals(task.workOrderNo?.Trim(), workOrderNo.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
            var targets = matched.Count > 0 ? matched : tasks;

            foreach (var task in targets)
            {
                task.stopLengthRecords = records;
            }
        }
        catch
        {
            // 停工记录属于附属信息，查询异常时不打断任务池展示。
        }
    }

    private async void OnInstructionTapped(object sender, TappedEventArgs e)
    {
        var workOrderId = _productionContext.Current?.WorkOrderId;
        if (string.IsNullOrWhiteSpace(workOrderId))
        {
            await DisplayAlert("提示", "当前生产工单为空，无法查看生产作业指令卡。", "确定");
            return;
        }

        await Shell.Current.GoToAsync($"{AppShell.RouteWorkOrderInstruction}?id={Uri.EscapeDataString(workOrderId)}");
    }

    private async void OnMaterialLoadingTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(AppShell.RouteMaterialLoading);
    }

    private async void OnMaterialUnloadingTapped(object sender, TappedEventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync(AppShell.RouteMaterialUnloading);
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "跳转失败", ex.Message, "确定");
        }
    }

    private async void OnManualUnloadingTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(AppShell.RoutePicklingInputRecords);
    }

    private async void OnAbnormalReportTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(AppShell.RouteAbnormalReport);
    }

    private async void OnReworkReportTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(AppShell.RouteReworkReport);
    }

    private async void OnPauseTapped(object sender, TappedEventArgs e)
    {
        var workOrderNo = _productionContext.Current?.WorkOrderNo;
        if (string.IsNullOrWhiteSpace(workOrderNo))
        {
            await DisplayAlert("提示", "当前生产工单为空，无法暂停生产工单。", "确定");
            return;
        }

        // 停工接口需要产出长度数量、长度单位与备注，先进入暂停作业页面采集参数。
        await Shell.Current.GoToAsync(AppShell.RouteWorkPause);
    }

    private async void OnWorkCompletionTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(AppShell.RouteWorkCompletion);
    }

    private async void OnSwitchMachineTapped(object sender, TappedEventArgs e)
    {
        if (_isOpeningMachineScanner || RefreshContainer.IsRefreshing || !ReferenceEquals(Shell.Current.CurrentPage, this)) return;
        _isOpeningMachineScanner = true;
        try
        {
            await Shell.Current.GoToAsync($"{AppShell.RouteWorkStartScan}?switchMachine=true");
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "跳转失败", ex.Message, "确定");
        }
        finally
        {
            _isOpeningMachineScanner = false;
        }
    }

    private async void OnBackHomeTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnQualityCodeConvertTapped(object sender, TappedEventArgs e)
    {
        try
        {
            // 与生产作业首页入口保持一致：生产端进入质检转码页时启用摄像头扫码。
            await Shell.Current.GoToAsync($"{AppShell.RouteQualityCodeConvert}?{CameraScanService.EnableCameraParameter}=true");
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "跳转失败", ex.Message, "确定");
        }
    }
}
