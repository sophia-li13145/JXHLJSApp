using JXHLJSApp.Models.Quality;
using JXHLJSApp.Services;
using JXHLJSApp.Services.Quality;

namespace JXHLJSApp.Pages.Quality;

public partial class ProductionQualityOrderListPage : ContentPage
{
    private readonly IQualityApi _qualityApi;
    private readonly IScanService _scanService;
    private readonly List<ProductionQualityStatusOption> _statuses = new()
    {
        new("所有状态", null),
        new("新建", "0"),
        new("待检验", "1"),
        new("检验中", "2"),
        new("检验完成", "3")
    };

    public ProductionQualityOrderListPage(IQualityApi qualityApi, IScanService scanService)
    {
        InitializeComponent();
        _qualityApi = qualityApi;
        _scanService = scanService;
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
            OrderList.ItemsSource = await _qualityApi.GetProductionQualityOrdersAsync(ResourceNameEntry.Text?.Trim(), status?.Value, QualityNoEntry.Text?.Trim());
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
        QualityNoEntry.Text = string.Empty;
        StatusPicker.SelectedIndex = 0;
        await LoadOrdersAsync();
    }

    private async void OnFilterClicked(object sender, EventArgs e) => await LoadOrdersAsync();

    private void OnAddManualInspectionClicked(object sender, EventArgs e)
    {
        ScanOverlay.IsVisible = true;
    }

    private void OnCancelScanClicked(object sender, EventArgs e)
    {
        ScanOverlay.IsVisible = false;
    }

    private async void OnScanManualMaterialClicked(object sender, TappedEventArgs e)
    {
        ScanOverlay.IsVisible = false;
        var qrCode = await _scanService.ScanAsync("扫描新增巡检");
        if (string.IsNullOrWhiteSpace(qrCode)) return;

        try
        {
            var detail = await _qualityApi.CreateManualInspectionAsync(qrCode.Trim());
            var qualityNo = detail.qualityNo;
            if (string.IsNullOrWhiteSpace(qualityNo))
            {
                await DisplayAlert("创建成功", "巡检单已创建，但接口未返回巡检单号。", "确定");
                await LoadOrdersAsync();
                return;
            }

            await DisplayAlert("创建成功", "巡检任务已创建。", "确定");
            await Shell.Current.GoToAsync($"{AppShell.RouteMachineQualityDetail}?qualityNo={Uri.EscapeDataString(qualityNo)}&workOrderNo={Uri.EscapeDataString(detail.workOrderNo ?? string.Empty)}&inspectStatus={Uri.EscapeDataString(detail.inspectStatus ?? string.Empty)}&manualInspection=true");
        }
        catch (Exception ex)
        {
            await DisplayAlert("创建失败", ex.Message, "确定");
        }
    }

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
