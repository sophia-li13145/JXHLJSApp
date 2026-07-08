using JXHLJSApp.Models.Warehouse;
using JXHLJSApp.Services.Warehouse;

namespace JXHLJSApp.Pages.Warehouse;

public partial class PackagingSubTaskListPage : ContentPage
{
    private readonly IWarehouseApi _warehouseApi;

    public PackagingSubTaskListPage(IWarehouseApi warehouseApi)
    {
        InitializeComponent();
        _warehouseApi = warehouseApi;
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
            TaskList.ItemsSource = await _warehouseApi.GetPackagingSubTaskListAsync();
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

    private async void OnTaskTapped(object sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not PackagingSubTaskDto item)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(item.id))
        {
            await DisplayAlert("提示", "未找到子工序任务ID，无法查看详情。", "确定");
            return;
        }

        await Shell.Current.GoToAsync($"{AppShell.RoutePackagingSubTaskDetail}?id={Uri.EscapeDataString(item.id)}");
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
