using JXHLJSApp.Models.WorkOrders;
using JXHLJSApp.Services.WorkOrders;

namespace JXHLJSApp.Pages.WorkStart;

[QueryProperty(nameof(WorkOrderId), "id")]
public partial class WorkExecutionPage : ContentPage
{
    private readonly IWorkOrderApi _workOrderApi;
    private List<WorkOrderDetailDto> _tasks = new();
    private string? _workOrderId;

    public string? WorkOrderId
    {
        get => _workOrderId;
        set => _workOrderId = Uri.UnescapeDataString(value ?? string.Empty);
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
            if (string.IsNullOrWhiteSpace(_workOrderId))
            {
                await DisplayAlert("提示", "工单列表主键为空，无法查询当前关联任务池。", "确定");
                TaskPoolList.ItemsSource = Array.Empty<WorkOrderDetailDto>();
                return;
            }

            RefreshContainer.IsRefreshing = true;
            var detail = await _workOrderApi.GetWorkOrderDetailAsync(_workOrderId);
            _tasks = detail is null ? new List<WorkOrderDetailDto>() : new List<WorkOrderDetailDto> { detail };
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

    private async void OnBackHomeTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(AppShell.RouteHome);
    }
}
