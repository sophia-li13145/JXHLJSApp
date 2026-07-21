using JXHLJSApp.Services;
using JXHLJSApp.Models;
using JXHLJSApp.Services.Transport;

namespace JXHLJSApp.Pages.Transport;

public partial class ProductInstockTransportOrderListPage : ContentPage
{
    private readonly ITransportOrderApi _transportOrderApi;

    public ProductInstockTransportOrderListPage(ITransportOrderApi transportOrderApi)
    {
        InitializeComponent();
        _transportOrderApi = transportOrderApi;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadOrdersAsync();
    }

    private async Task LoadOrdersAsync()
    {
        try
        {
            OrderList.ItemsSource = await _transportOrderApi.GetProductInstockTransportOrdersAsync();
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "加载失败", ex.Message, "确定");
        }
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        await LoadOrdersAsync();
        RefreshContainer.IsRefreshing = false;
    }

    private async void OnOrderTapped(object sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not ProductInstockTransportOrderDto item)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(item.transportOrderNo))
        {
            await DisplayAlert("提示", "未找到运输单号，无法查看详情。", "确定");
            return;
        }

        await Shell.Current.GoToAsync($"{AppShell.RouteProductInstockTransportOrderDetail}?transportOrderNo={Uri.EscapeDataString(item.transportOrderNo)}");
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
