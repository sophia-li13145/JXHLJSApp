using JXHLJSApp.Models.WorkOrders;
using JXHLJSApp.Services;
using JXHLJSApp.Services.WorkOrders;

namespace JXHLJSApp.Pages.WorkStart;

public partial class WorkExecutionPage : ContentPage
{
    private readonly IWorkOrderApi _workOrderApi;
    private readonly IScanService _scanService;
    private readonly IProductionContextService _productionContext;
    private List<WorkOrderDetailDto> _tasks = new();
    public WorkExecutionPage(
        IWorkOrderApi workOrderApi,
        IScanService scanService,
        IProductionContextService productionContext)
    {
        InitializeComponent();
        _workOrderApi = workOrderApi;
        _scanService = scanService;
        _productionContext = productionContext;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadTaskPoolAsync();
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        await LoadTaskPoolAsync();
    }

    private async Task LoadTaskPoolAsync()
    {
        try
        {
            var workOrderNo = _productionContext.Current?.WorkOrderNo;
            if (string.IsNullOrWhiteSpace(workOrderNo))
            {
                await DisplayAlert("提示", "当前生产工单为空，无法查询当前关联任务池。", "确定");
                TaskPoolList.ItemsSource = Array.Empty<WorkOrderDetailDto>();
                return;
            }

            RefreshContainer.IsRefreshing = true;
            _tasks = await _workOrderApi.GetCurrentTaskPoolAsync(workOrderNo);
            TaskPoolList.ItemsSource = _tasks;
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "查询失败", ex.Message, "确定");
        }
        finally
        {
            RefreshContainer.IsRefreshing = false;
        }
    }

    private void UpdateProductionContextMachine(string machineCode)
    {
        var current = _productionContext.Current;
        if (current is null)
        {
            return;
        }

        _productionContext.Set(new ProductionContext
        {
            WorkOrderId = current.WorkOrderId,
            WorkOrderNo = current.WorkOrderNo,
            ExecutionId = current.ExecutionId,
            MachineCode = machineCode,
            Status = current.Status,
            StartedAt = current.StartedAt,
            SessionId = current.SessionId
        });
    }

    private void UpdateProductionContextStatus(string status)
    {
        var current = _productionContext.Current;
        if (current is null)
        {
            return;
        }

        _productionContext.Set(new ProductionContext
        {
            WorkOrderId = current.WorkOrderId,
            WorkOrderNo = current.WorkOrderNo,
            ExecutionId = current.ExecutionId,
            MachineCode = current.MachineCode,
            Status = status,
            StartedAt = current.StartedAt,
            SessionId = current.SessionId
        });
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

        var confirm = await DisplayAlert("确认暂停", $"确认暂停工单 {workOrderNo} 吗？", "确认", "取消");
        if (!confirm)
        {
            return;
        }

        try
        {
            RefreshContainer.IsRefreshing = true;
            var success = await _workOrderApi.StopWorkOrderAsync(workOrderNo);
            if (!success)
            {
                await ErrorDialogService.ShowAsync(this, "暂停失败", "接口未返回成功，请稍后重试。", "确定");
                return;
            }

            UpdateProductionContextStatus("Paused");
            await LoadTaskPoolAsync();
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "暂停失败", ex.Message, "确定");
        }
        finally
        {
            RefreshContainer.IsRefreshing = false;
        }
    }

    private async void OnWorkCompletionTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(AppShell.RouteWorkCompletion);
    }

    private async void OnSwitchMachineTapped(object sender, TappedEventArgs e)
    {
        var code = await _scanService.ScanAsync("扫码切换机台");
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        await BindMachineAndReturnHomeAsync(code);
    }

    private async Task BindMachineAndReturnHomeAsync(string machineCode)
    {
        var devCode = machineCode.Trim();
        if (string.IsNullOrWhiteSpace(devCode))
        {
            await DisplayAlert("提示", "机台编号为空，无法切换机台。", "确定");
            return;
        }

        try
        {
            RefreshContainer.IsRefreshing = true;
            var success = await _workOrderApi.BindWorkerMachineAsync(devCode);
            if (!success)
            {
                await ErrorDialogService.ShowAsync(this, "切换失败", "机台绑定未成功，请确认机台编号后重试。", "确定");
                return;
            }

            UpdateProductionContextMachine(devCode);
            await Shell.Current.GoToAsync(AppShell.RouteHome);
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "切换失败", ex.Message, "确定");
        }
        finally
        {
            RefreshContainer.IsRefreshing = false;
        }
    }

    private async void OnBackHomeTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
