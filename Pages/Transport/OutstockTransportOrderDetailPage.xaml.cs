using JXHLJSApp.Services;
using JXHLJSApp.Services.Transport;
using System.Collections.ObjectModel;

namespace JXHLJSApp.Pages.Transport;

[QueryProperty(nameof(TransportOrderNo), "transportOrderNo")]
public partial class OutstockTransportOrderDetailPage : ContentPage
{
    private readonly ITransportOrderApi _transportOrderApi;
    private readonly ObservableCollection<Models.MaterialOutstockTransportOrderDetailItemDto> _items = new();
    private string? _transportOrderNo;

    public string? TransportOrderNo
    {
        get => _transportOrderNo;
        set => _transportOrderNo = Uri.UnescapeDataString(value ?? string.Empty);
    }

    public OutstockTransportOrderDetailPage(ITransportOrderApi transportOrderApi)
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
            var detail = await _transportOrderApi.GetMaterialOutstockTransportOrderDetailAsync(_transportOrderNo);
            BindDetail(detail);
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "加载失败", ex.Message, "确定");
        }
    }

    private void BindDetail(Models.MaterialOutstockTransportOrderDetailDto detail)
    {
        BindingContext = detail;

        _items.Clear();
        foreach (var item in detail.detailList ?? new List<Models.MaterialOutstockTransportOrderDetailItemDto>())
        {
            _items.Add(item);
        }
    }

    private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "--" : value!;

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnBackButtonClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
