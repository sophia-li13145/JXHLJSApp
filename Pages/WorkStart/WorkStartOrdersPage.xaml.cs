using JXHLJSApp.Models.WorkOrders;
using JXHLJSApp.Services.WorkOrders;

namespace JXHLJSApp.Pages.WorkStart;

public partial class WorkStartOrdersPage : ContentPage
{
    private readonly IWorkOrderApi _workOrderApi;
    private readonly IProductionContextService _productionContext;
    private List<WorkOrderTaskDto> _orders = new();

    public WorkStartOrdersPage(
        IWorkOrderApi workOrderApi,
        IProductionContextService productionContext)
    {
        InitializeComponent();
        _workOrderApi = workOrderApi;
        _productionContext = productionContext;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadOrdersAsync();
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        await LoadOrdersAsync();
    }

    private async Task LoadOrdersAsync()
    {
        try
        {
            RefreshContainer.IsRefreshing = true;
            _orders = await _workOrderApi.GetCurrentUserMachinesWorkOrdersAsync();
            OrdersList.ItemsSource = _orders;
            HeaderLabel.Text = BuildHeaderText(_orders);
        }
        catch (Exception ex)
        {
            await DisplayAlert("查询失败", ex.Message, "确定");
        }
        finally
        {
            RefreshContainer.IsRefreshing = false;
        }
    }

    private static string BuildHeaderText(IReadOnlyList<WorkOrderTaskDto> orders)
    {
        if (orders.Count == 0) return "当前绑定机台暂无待执行计划工单。";

        var first = orders[0];
        var machine = string.IsNullOrWhiteSpace(first.deviceCode) ? "--" : first.deviceCode;
        return $"机台 {machine} 待执行计划工单。请选择 {orders.Count} 个工单开工。";
    }

    private async void OnRescanTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
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

        try
        {
            var result = await _workOrderApi.StartWorkOrderAsync(order.workOrderNo);
            if (!result)
            {
                await DisplayAlert("开工失败", "接口返回开工失败，请稍后重试。", "确定");
                return;
            }

            var context = new ProductionContext
            {
                WorkOrderId = order.id,
                WorkOrderNo = order.workOrderNo,
                MachineCode = order.deviceCode,
                Status = "Running",
                StartedAt = DateTime.Now
            };

            _productionContext.Set(context);

            await Shell.Current.GoToAsync(AppShell.RouteWorkExecution);
        }
        catch (Exception ex)
        {
            await DisplayAlert("开工失败", ex.Message, "确定");
        }
    }
}
