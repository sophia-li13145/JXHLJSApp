using JXHLJSApp.Models.Warehouse;
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

    private async void OnCancelInstockClicked(object sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not RawMaterialReceivingDto item || string.IsNullOrWhiteSpace(item.id))
        {
            await DisplayAlert("提示", "未找到可取消的入库单。", "确定");
            return;
        }

        var confirm = await DisplayAlert("确认取消", $"确定取消入库单 {item.instockNoDisplay} 吗？", "确定", "返回");
        if (!confirm) return;

        try
        {
            await _warehouseApi.CancelBlankInstockAsync(item.id);
            await LoadReceivingListAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("取消失败", ex.Message, "确定");
        }
    }

    private async void OnReceivingItemTapped(object sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not RawMaterialReceivingDto item)
        {
            return;
        }

        var instockNo = item.instockNo?.Trim();
        if (string.IsNullOrWhiteSpace(instockNo))
        {
            await DisplayAlert("提示", "未找到入库单号，无法查看详情。", "确定");
            return;
        }

        var route = item.canCancel ? AppShell.RouteAddRawMaterialReceiving : AppShell.RouteRawMaterialReceivingDetail;
        await Shell.Current.GoToAsync($"{route}?InstockNo={Uri.EscapeDataString(instockNo)}");
    }

    private async void OnAddClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync(AppShell.RouteAddRawMaterialReceiving);
}
