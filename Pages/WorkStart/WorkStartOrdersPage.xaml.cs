using JXHLJSApp.Models.WorkOrders;
using JXHLJSApp.Services.WorkOrders;

namespace JXHLJSApp.Pages.WorkStart;

public partial class WorkStartOrdersPage : ContentPage
{
    private readonly IWorkOrderApi _workOrderApi;
    private List<WorkOrderTaskDto> _orders = new();

    public WorkStartOrdersPage(IWorkOrderApi workOrderApi)
    {
        InitializeComponent();
        _workOrderApi = workOrderApi;
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
        var machine = string.IsNullOrWhiteSpace(first.machineNo) ? first.deviceName : first.machineNo;
        return $"机台 {machine} 待执行计划工单。请选择 {orders.Count} 个工单开工。";
    }

    private async void OnRescanTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string workOrderNo } || string.IsNullOrWhiteSpace(workOrderNo))
        {
            await DisplayAlert("提示", "工单号为空，无法开工。", "确定");
            return;
        }

        try
        {
            var result = await _workOrderApi.StartWorkOrderAsync(workOrderNo);
            await DisplayAlert(result ? "开工成功" : "开工失败", result ? "已确认上机开工。" : "接口返回开工失败，请稍后重试。", "确定");
            if (result) await LoadOrdersAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("开工失败", ex.Message, "确定");
        }
    }
}
