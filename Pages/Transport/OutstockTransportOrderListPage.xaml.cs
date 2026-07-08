using JXHLJSApp.Models;
using JXHLJSApp.Services.Transport;

namespace JXHLJSApp.Pages.Transport;

public partial class OutstockTransportOrderListPage : ContentPage
{
    private readonly ITransportOrderApi _transportOrderApi;

    public OutstockTransportOrderListPage(ITransportOrderApi transportOrderApi)
    {
        InitializeComponent();
        _transportOrderApi = transportOrderApi;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadOrdersAsync();
    }

    private async void OnRefreshing(object sender, EventArgs e) => await LoadOrdersAsync();

    private async Task LoadOrdersAsync()
    {
        try
        {
            RefreshContainer.IsRefreshing = true;
            OrderList.ItemsSource = await _transportOrderApi.GetMaterialOutstockTransportOrdersAsync();
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

    private async void OnOrderTapped(object sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not MaterialOutstockTransportOrderDto item)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(item.transportOrderNo))
        {
            await DisplayAlert("提示", "未找到运输单号，无法查看详情。", "确定");
            return;
        }

        await Shell.Current.GoToAsync($"{AppShell.RouteOutstockTransportOrderDetail}?transportOrderNo={Uri.EscapeDataString(item.transportOrderNo)}");
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
