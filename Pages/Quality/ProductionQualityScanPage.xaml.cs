using JXHLJSApp.Services;

namespace JXHLJSApp.Pages.Quality;

public partial class ProductionQualityScanPage : ContentPage
{
    private readonly IScanService _scanService;
    private bool _isScanning;

    public ProductionQualityScanPage(IScanService scanService)
    {
        InitializeComponent();
        _scanService = scanService;
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnScanTapped(object sender, TappedEventArgs e)
    {
        if (_isScanning) return;
        _isScanning = true;
        try
        {
            var code = await _scanService.ScanAsync("扫描物料或工单二维码");
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
}
