using JXHLJSApp.Models.Quality;
using JXHLJSApp.Services.Quality;

namespace JXHLJSApp.Pages.Quality;

public partial class ProductionQualityOrderListPage : ContentPage
{
    private readonly IQualityApi _qualityApi;
    private readonly List<ProductionQualityStatusOption> _statuses = new()
    {
        new("所有状态", null),
        new("新建", "0"),
        new("待检验", "1"),
        new("检验中", "2"),
        new("检验完成", "3")
    };

    public ProductionQualityOrderListPage(IQualityApi qualityApi)
    {
        InitializeComponent();
        _qualityApi = qualityApi;
        StatusPicker.ItemsSource = _statuses;
        StatusPicker.SelectedIndex = 0;
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
            var status = StatusPicker.SelectedItem as ProductionQualityStatusOption;
            OrderList.ItemsSource = await _qualityApi.GetProductionQualityOrdersAsync(ResourceNameEntry.Text?.Trim(), status?.Value);
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

    private async void OnResetClicked(object sender, EventArgs e)
    {
        ResourceNameEntry.Text = string.Empty;
        StatusPicker.SelectedIndex = 0;
        await LoadOrdersAsync();
    }

    private async void OnFilterClicked(object sender, EventArgs e) => await LoadOrdersAsync();

    private async void OnOrderTapped(object sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not ProductionQualityOrderDto item) return;
        if (string.IsNullOrWhiteSpace(item.qualityNo) || string.IsNullOrWhiteSpace(item.orderNumber))
        {
            await DisplayAlert("提示", "质检单号或工单号为空，无法查询详情。", "确定");
            return;
        }

        await Shell.Current.GoToAsync($"{AppShell.RouteMachineQualityDetail}?qualityNo={Uri.EscapeDataString(item.qualityNo)}&workOrderNo={Uri.EscapeDataString(item.orderNumber)}&inspectStatus={Uri.EscapeDataString(item.inspectStatus ?? string.Empty)}");
    }

    private sealed record ProductionQualityStatusOption(string Name, string? Value);
}
