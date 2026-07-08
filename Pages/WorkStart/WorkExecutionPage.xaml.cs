using JXHLJSApp.Models.WorkOrders;
using JXHLJSApp.Services.WorkOrders;

namespace JXHLJSApp.Pages.WorkStart;

[QueryProperty(nameof(WorkOrderId), "id")]
[QueryProperty(nameof(WorkOrderNo), "workOrderNo")]
public partial class WorkExecutionPage : ContentPage
{
    private readonly IWorkOrderApi _workOrderApi;
    private List<WorkOrderDetailDto> _tasks = new();
    private string? _workOrderId;
    private string? _workOrderNo;

    public string? WorkOrderId
    {
        get => _workOrderId;
        set => _workOrderId = Uri.UnescapeDataString(value ?? string.Empty);
    }

    public string? WorkOrderNo
    {
        get => _workOrderNo;
        set => _workOrderNo = Uri.UnescapeDataString(value ?? string.Empty);
    }

    public WorkExecutionPage(IWorkOrderApi workOrderApi)
    {
        InitializeComponent();
        _workOrderApi = workOrderApi;
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
            if (string.IsNullOrWhiteSpace(_workOrderNo))
            {
                await DisplayAlert("提示", "工单号为空，无法查询当前关联任务池。", "确定");
                TaskPoolList.ItemsSource = Array.Empty<WorkOrderDetailDto>();
                return;
            }

            RefreshContainer.IsRefreshing = true;
            _tasks = await _workOrderApi.GetCurrentTaskPoolAsync(_workOrderNo);
            TaskPoolList.ItemsSource = _tasks;
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

    private async void OnInstructionTapped(object sender, TappedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_workOrderId))
        {
            await DisplayAlert("提示", "工单列表主键为空，无法查看生产作业指令卡。", "确定");
            return;
        }

        var query = new Dictionary<string, object>
        {
            ["id"] = _workOrderId
        };

        if (!string.IsNullOrWhiteSpace(_workOrderNo))
        {
            query["workOrderNo"] = _workOrderNo;
        }

        await Shell.Current.GoToAsync(AppShell.RouteWorkOrderInstruction, query);
    }

    private async void OnMaterialLoadingTapped(object sender, TappedEventArgs e)
    {
        var query = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(_workOrderId))
        {
            query["id"] = _workOrderId;
        }

        if (!string.IsNullOrWhiteSpace(_workOrderNo))
        {
            query["workOrderNo"] = _workOrderNo;
        }

        await Shell.Current.GoToAsync(AppShell.RouteMaterialLoading, query);
    }

    private async void OnAbnormalReportTapped(object sender, TappedEventArgs e)
    {
        var query = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(_workOrderNo))
        {
            query["workOrderNo"] = _workOrderNo;
        }

        await Shell.Current.GoToAsync(AppShell.RouteAbnormalReport, query);
    }

    private async void OnReworkReportTapped(object sender, TappedEventArgs e)
    {
        var query = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(_workOrderNo))
        {
            query["workOrderNo"] = _workOrderNo;
        }

        await Shell.Current.GoToAsync(AppShell.RouteReworkReport, query);
    }

    private async void OnBackHomeTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
