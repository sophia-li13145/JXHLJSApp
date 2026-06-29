using JXHLJSApp.Services;
using JXHLJSApp.Services.WorkOrders;

namespace JXHLJSApp.Pages.WorkStart;

public partial class WorkStartScanPage : ContentPage
{
    private readonly IWorkOrderApi _workOrderApi;
    private readonly IScanService _scanService;

    public WorkStartScanPage(IWorkOrderApi workOrderApi, IScanService scanService)
    {
        InitializeComponent();
        _workOrderApi = workOrderApi;
        _scanService = scanService;
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnScanTapped(object sender, TappedEventArgs e)
    {
        var code = await _scanService.ScanAsync("扫码机台二维码上机");
        if (!string.IsNullOrWhiteSpace(code))
        {
            await BindMachineAndOpenOrdersAsync(code);
        }
    }

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        await BindMachineAndOpenOrdersAsync(MachineNoEntry.Text);
    }

    private async Task BindMachineAndOpenOrdersAsync(string? machineCode)
    {
        var devCode = machineCode?.Trim();
        if (string.IsNullOrWhiteSpace(devCode))
        {
            await DisplayAlert("提示", "请输入机台编号", "确定");
            return;
        }

        try
        {
            var result = await _workOrderApi.BindWorkerMachineAsync(devCode);
            if (!result)
            {
                await DisplayAlert("绑定失败", "机台绑定未成功，请确认机台编号后重试。", "确定");
                return;
            }

            await Shell.Current.GoToAsync(AppShell.RouteWorkStartOrders);
        }
        catch (Exception ex)
        {
            await DisplayAlert("绑定失败", ex.Message, "确定");
        }
    }
}
