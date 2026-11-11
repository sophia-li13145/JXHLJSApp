using JXHLJSApp.Services;

namespace JXHLJSApp.Pages.Quality;

public partial class ProductionQualityScanPage : ContentPage
{
    private readonly IRightKeyScanService _scanner;
    private object? _scanOperation;

    public ProductionQualityScanPage(IRightKeyScanService scanner)
    {
        InitializeComponent();
        _scanner = scanner;
        _scanner.Register(this,
            code => ScanAndOpenOrdersAsync(code),
            () => _scanOperation is null);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // 返回扫码入口后开启新会话，不等待上一轮导航任务结束。
        _scanOperation = null;
        _scanner.RefreshInput(this);
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

        if (_scanOperation is not null) return;
        var operation = new object();
        _scanOperation = operation;
        try
        {
            await Shell.Current.GoToAsync($"{AppShell.RouteProductionQualityOrders}?qrCode={Uri.EscapeDataString(scanCode.Trim())}");
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(_scanOperation, operation))
                await ErrorDialogService.ShowAsync(this, "扫码查询失败", ex.Message, "确定");
        }
        finally
        {
            if (ReferenceEquals(_scanOperation, operation))
            {
                _scanOperation = null;
                _scanner.RefreshInput(this);
            }
        }
    }
}
