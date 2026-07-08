using JXHLJSApp.Services.Transport;
using System.Collections.ObjectModel;

namespace JXHLJSApp.Pages.Transport;

[QueryProperty(nameof(TransportOrderNo), "transportOrderNo")]
public partial class ProductInstockTransportOrderDetailPage : ContentPage
{
    private readonly ITransportOrderApi _transportOrderApi;
    private readonly ObservableCollection<Models.ProductInstockTransportOrderDetailItemDto> _items = new();
    private string? _transportOrderNo;

    public string? TransportOrderNo
    {
        get => _transportOrderNo;
        set => _transportOrderNo = Uri.UnescapeDataString(value ?? string.Empty);
    }

    public ProductInstockTransportOrderDetailPage(ITransportOrderApi transportOrderApi)
    {
        InitializeComponent();
        _transportOrderApi = transportOrderApi;
        DetailList.ItemsSource = _items;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDetailAsync();
    }

    private async Task LoadDetailAsync()
    {
        if (string.IsNullOrWhiteSpace(_transportOrderNo))
        {
            await DisplayAlert("提示", "运输单号为空，无法查询详情。", "确定");
            return;
        }

        try
        {
            var detail = await _transportOrderApi.GetProductInstockTransportOrderDetailAsync(_transportOrderNo);
            BindDetail(detail);
        }
        catch (Exception ex)
        {
            await DisplayAlert("加载失败", ex.Message, "确定");
        }
    }

    private void BindDetail(Models.ProductInstockTransportOrderDetailDto detail)
    {
        TransportOrderNoLabel.Text = detail.orderNoDisplay;
        RouteLabel.Text = detail.routeDisplay;

        _items.Clear();
        foreach (var item in detail.detailList ?? new List<Models.ProductInstockTransportOrderDetailItemDto>())
        {
            _items.Add(item);
        }
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnBackToListClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
