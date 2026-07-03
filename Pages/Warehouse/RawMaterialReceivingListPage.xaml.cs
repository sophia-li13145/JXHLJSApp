using JXHLJSApp.Services.Warehouse;

namespace JXHLJSApp.Pages.Warehouse;

public partial class RawMaterialReceivingListPage : ContentPage
{
    private readonly IWarehouseApi _warehouseApi;

    public RawMaterialReceivingListPage(IWarehouseApi warehouseApi)
    {
        InitializeComponent();
        _warehouseApi = warehouseApi;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadReceivingListAsync();
    }

    private async void OnRefreshing(object sender, EventArgs e) => await LoadReceivingListAsync();

    private async Task LoadReceivingListAsync()
    {
        try
        {
            RefreshContainer.IsRefreshing = true;
            ReceivingList.ItemsSource = await _warehouseApi.GetRawMaterialReceivingListAsync();
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

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnAddClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync(AppShell.RouteAddRawMaterialReceiving);
}
