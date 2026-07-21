using JXHLJSApp.Services;
using JXHLJSApp.Models.Warehouse;
using JXHLJSApp.Services.Warehouse;

namespace JXHLJSApp.Pages.Warehouse;

public partial class DeliveryOrderListPage : ContentPage
{
    private readonly IWarehouseApi _warehouseApi;

    public DeliveryOrderListPage(IWarehouseApi warehouseApi)
    {
        InitializeComponent();
        _warehouseApi = warehouseApi;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDeliveryOrdersAsync();
    }

    private async void OnRefreshing(object sender, EventArgs e) => await LoadDeliveryOrdersAsync();

    private async Task LoadDeliveryOrdersAsync()
    {
        try
        {
            RefreshContainer.IsRefreshing = true;
            DeliveryOrderList.ItemsSource = await _warehouseApi.GetNeedDeliveryOrdersAsync();
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "加载失败", ex.Message, "确定");
        }
        finally
        {
            RefreshContainer.IsRefreshing = false;
        }
    }

    private async void OnDeliveryOrderTapped(object sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not DeliveryOrderDto item)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(item.deliveryNo))
        {
            await DisplayAlert("提示", "未找到发货单号，无法查看详情。", "确定");
            return;
        }

        await Shell.Current.GoToAsync($"{AppShell.RouteDeliveryOrderDetail}?deliveryNo={Uri.EscapeDataString(item.deliveryNo)}");
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
