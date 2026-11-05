using JXHLJSApp.Services;

namespace JXHLJSApp.Pages.Quality;

public partial class ProductionQualityScanPage : ContentPage
{
    private readonly IRightKeyScanService _scanner;
    private bool _isHandlingScan;
    private bool _isScanning;

    public ProductionQualityScanPage(IRightKeyScanService scanner)
    {
        InitializeComponent();
        _scanner = scanner;
        _scanner.Register(this,
            code => ScanAndOpenOrdersAsync(code),
            () => !_isHandlingScan);
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnScanTapped(object sender, TappedEventArgs e) => await ScanAndOpenOrdersAsync();

    private async Task ScanAndOpenOrdersAsync(string? scanCode = null)
    {
        if (string.IsNullOrWhiteSpace(scanCode))
        {
            await _scanner.ShowHintAsync(this, "扫描物料或工单二维码");
            return;
        }

        if (_isHandlingScan) return;
        _isHandlingScan = true;
        try
        {
            if (_isScanning) return;
            _isScanning = true;
            try
            {
                var code = scanCode;
                if (string.IsNullOrWhiteSpace(code)) return;

                await Shell.Current.GoToAsync($"{AppShell.RouteProductionQualityOrders}?qrCode={Uri.EscapeDataString(code.Trim())}");
            }
            catch (Exception ex)
            {
                await ErrorDialogService.ShowAsync(this, "扫码查询失败", ex.Message, "确定");
            }
            finally
            {
                _isScanning = false;
            }
        }
        finally
        {
            _isHandlingScan = false;
        }
    }
}
