using JXHLJSApp.Models.WorkOrders;
using JXHLJSApp.Services;
using JXHLJSApp.Services.WorkOrders;

namespace JXHLJSApp.Pages.WorkStart;

public partial class PicklingInputRecordListPage : ContentPage
{
    private readonly IWorkOrderApi _workOrderApi;
    private readonly IProductionContextService _productionContext;
    private bool _isNavigating;

    public PicklingInputRecordListPage(IWorkOrderApi workOrderApi, IProductionContextService productionContext)
    {
        InitializeComponent();
        _workOrderApi = workOrderApi;
        _productionContext = productionContext;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!ErrorDialogService.IsDialogVisible) await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var workOrderNo = _productionContext.Current?.WorkOrderNo;
        if (string.IsNullOrWhiteSpace(workOrderNo))
        {
            RecordList.ItemsSource = Array.Empty<PicklingInputRecordDto>();
            await DisplayAlert("提示", "当前生产工单为空，无法查询上料作业记录。", "确定");
            return;
        }

        try
        {
            RefreshContainer.IsRefreshing = true;
            RecordList.ItemsSource = await _workOrderApi.GetPicklingInputRecordListAsync(workOrderNo);
        }
        catch (Exception ex) { await ErrorDialogService.ShowAsync(this, "查询失败", ex.Message, "确定"); }
        finally { RefreshContainer.IsRefreshing = false; }
    }

    private async void OnRefreshing(object sender, EventArgs e) => await LoadAsync();
    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnUnloadClicked(object sender, EventArgs e)
    {
        if (_isNavigating)
        {
            return;
        }

        if (sender is not Button { BindingContext: PicklingInputRecordDto record } ||
            string.IsNullOrWhiteSpace(record.inputRecordId) || string.IsNullOrWhiteSpace(record.sourceQrCode))
        {
            await DisplayAlert("提示", "当前记录缺少上料记录 ID 或二维码，无法下料。", "确定");
            return;
        }

        var workOrderNo = _productionContext.Current?.WorkOrderNo;
        if (string.IsNullOrWhiteSpace(workOrderNo))
        {
            await DisplayAlert("提示", "当前生产工单为空，无法进入下料作业。", "确定");
            return;
        }

        try
        {
            _isNavigating = true;
            await Shell.Current.GoToAsync($"{AppShell.RouteMaterialUnloading}?manual=true&inputRecordId={Uri.EscapeDataString(record.inputRecordId)}&qrCode={Uri.EscapeDataString(record.sourceQrCode)}&workOrderNo={Uri.EscapeDataString(workOrderNo)}");
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "跳转失败", ex.Message, "确定");
        }
        finally
        {
            _isNavigating = false;
        }
    }
}
