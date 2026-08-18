using JXHLJSApp.Services;
using JXHLJSApp.Models;
using JXHLJSApp.Services.Transport;
using System.Collections.ObjectModel;

namespace JXHLJSApp.Pages.Transport;

public partial class OutstockTransportOrderListPage : ContentPage
{
    private readonly ITransportOrderApi _transportOrderApi;
    private readonly ObservableCollection<MaterialOutstockTransportOrderDto> _orders = new();
    private const long PageSize = 10;
    private long _nextPageNo = 1;
    private bool _isLoading;
    private bool _hasMore = true;

    public OutstockTransportOrderListPage(ITransportOrderApi transportOrderApi)
    {
        InitializeComponent();
        _transportOrderApi = transportOrderApi;
        OrderList.ItemsSource = _orders;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Closing a modal error dialog reactivates the underlying page on Android.
        // Do not treat that lifecycle callback as a new backend operation.
        if (ErrorDialogService.IsDialogVisible)
        {
            return;
        }
        await LoadOrdersAsync(reset: true);
    }

    private async void OnRefreshing(object sender, EventArgs e) => await LoadOrdersAsync(reset: true);

    private async void OnRemainingItemsThresholdReached(object sender, EventArgs e) => await LoadOrdersAsync(reset: false);

    private async Task LoadOrdersAsync(bool reset)
    {
        if (_isLoading || (!reset && !_hasMore))
        {
            return;
        }

        _isLoading = true;
        LoadingMoreIndicator.IsRunning = !reset;
        LoadingMoreIndicator.IsVisible = !reset;

        try
        {
            if (reset)
            {
                RefreshContainer.IsRefreshing = true;
                _nextPageNo = 1;
                _hasMore = true;
            }

            var page = await _transportOrderApi.GetMaterialOutstockTransportOrdersAsync(_nextPageNo, PageSize);
            var records = page.records ?? new List<MaterialOutstockTransportOrderDto>();

            if (reset)
            {
                _orders.Clear();
            }

            foreach (var order in records)
            {
                _orders.Add(order);
            }

            _nextPageNo++;
            _hasMore = records.Count == PageSize && (!page.total.HasValue || _orders.Count < page.total.Value);
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "加载失败", ex.Message, "确定");
        }
        finally
        {
            RefreshContainer.IsRefreshing = false;
            LoadingMoreIndicator.IsRunning = false;
            LoadingMoreIndicator.IsVisible = false;
            _isLoading = false;
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
