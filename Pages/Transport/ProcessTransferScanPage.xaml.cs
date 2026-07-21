using JXHLJSApp.Services;
using JXHLJSApp.Services.Transport;

namespace JXHLJSApp.Pages.Transport;

public partial class ProcessTransferScanPage : ContentPage
{
    private readonly IScanService _scanService;
    private readonly ITransportOrderApi _transportOrderApi;
    private bool _busy;

    public ProcessTransferScanPage(IScanService scanService, ITransportOrderApi transportOrderApi)
    {
        _scanService = scanService;
        _transportOrderApi = transportOrderApi;
        InitializeComponent();
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnScanTapped(object sender, TappedEventArgs e)
    {
        if (_busy) return;
        var code = await _scanService.ScanAsync("扫描物料二维码");
        if (string.IsNullOrWhiteSpace(code)) return;

        try
        {
            _busy = true;
            var order = await _transportOrderApi.ScanTransportOrderAsync(code.Trim());
            TransportOrderNavigationStore.Current = order;
            await Shell.Current.GoToAsync(AppShell.RouteProcessTransferConfirm);
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "获取转运单失败", ex.Message, "确定");
        }
        finally
        {
            _busy = false;
        }
    }
}
