using JXHLJSApp.Services;
using JXHLJSApp.Services.Transport;

namespace JXHLJSApp.Pages.Transport;

public partial class ProcessTransferScanPage : ContentPage
{
    private readonly IRightKeyScanService _scanner;
    private bool _isHandlingScan;
    private readonly ITransportOrderApi _transportOrderApi;
    private bool _busy;

    public ProcessTransferScanPage(IRightKeyScanService scanner, ITransportOrderApi transportOrderApi)
    {
        _scanner = scanner;
        _scanner.Register(this,
            code => ScanAndOpenTransferAsync(code),
            () => !_isHandlingScan && (!_busy));
        _transportOrderApi = transportOrderApi;
        InitializeComponent();
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnScanTapped(object sender, TappedEventArgs e) => await ScanAndOpenTransferAsync();

    private async Task ScanAndOpenTransferAsync(string? scanCode = null)
    {
        if (string.IsNullOrWhiteSpace(scanCode))
        {
            await _scanner.ShowHintAsync(this, "扫描物料二维码");
            return;
        }

        if (_isHandlingScan) return;
        _isHandlingScan = true;
        try
        {
            if (_busy) return;
            var code = scanCode;
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
        finally
        {
            _isHandlingScan = false;
        }
    }
}
