using JXHLJSApp.Models.WorkOrders;
using JXHLJSApp.Services.WorkOrders;

namespace JXHLJSApp.Pages.WorkStart;

public partial class WorkExecutionPage : ContentPage
{
    private readonly IWorkOrderApi _workOrderApi;
    private List<WorkOrderInputOutputDto> _tasks = new();

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
            RefreshContainer.IsRefreshing = true;
            _tasks = await _workOrderApi.GetWorkOrderInputOutputAsync();
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
