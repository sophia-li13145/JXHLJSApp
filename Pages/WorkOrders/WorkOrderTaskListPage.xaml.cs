using JXHLJSApp.Services.WorkOrders;

namespace JXHLJSApp.Pages.WorkOrders;

public partial class WorkOrderTaskListPage : ContentPage
{
    private readonly IWorkOrderApi _workOrderApi;

    public WorkOrderTaskListPage(IWorkOrderApi workOrderApi)
    {
        InitializeComponent();
        _workOrderApi = workOrderApi;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadTasksAsync();
    }

    private async void OnRefreshing(object sender, EventArgs e) => await LoadTasksAsync();

    private async Task LoadTasksAsync()
    {
        try
        {
            RefreshContainer.IsRefreshing = true;
            TaskList.ItemsSource = await _workOrderApi.GetWorkOrderListAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("加载失败", ex.Message, "确定");
        }
        finally
        {
            RefreshContainer.IsRefreshing = false;
        }
    }
}
