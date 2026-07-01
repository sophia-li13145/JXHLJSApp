using JXHLJSApp.Models.WorkOrders;
using JXHLJSApp.Services.WorkOrders;

namespace JXHLJSApp.Pages.WorkStart;

[QueryProperty(nameof(WorkOrderNo), "workOrderNo")]
public partial class WorkExecutionPage : ContentPage
{
    private readonly IWorkOrderApi _workOrderApi;
    private List<WorkOrderInputOutputDto> _tasks = new();
    private string? _workOrderNo;

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
                TaskPoolList.ItemsSource = Array.Empty<WorkOrderInputOutputDto>();
                return;
            }

            RefreshContainer.IsRefreshing = true;
            _tasks = await _workOrderApi.GetWorkOrderInputOutputAsync(_workOrderNo);
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
