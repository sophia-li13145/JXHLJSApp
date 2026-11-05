using JXHLJSApp.Services;
using JXHLJSApp.Services.Quality;

namespace JXHLJSApp.Pages.Quality;

public partial class MachineQualityScanPage : ContentPage
{
    private readonly IRightKeyScanService _scanner;
    private bool _isHandlingScan;
    private readonly IQualityApi _qualityApi;

    public MachineQualityScanPage(IRightKeyScanService scanner, IQualityApi qualityApi)
    {
        InitializeComponent();
        _scanner = scanner;
        _scanner.Register(this,
            code => ScanAndOpenTasksAsync(code),
            () => !_isHandlingScan);
        _qualityApi = qualityApi;
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnScanTapped(object sender, TappedEventArgs e) => await ScanAndOpenTasksAsync();

    private async Task ScanAndOpenTasksAsync(string? scanCode = null)
    {
        if (string.IsNullOrWhiteSpace(scanCode))
        {
            await _scanner.ShowHintAsync(this, "扫描机台二维码");
            return;
        }

        if (_isHandlingScan) return;
        _isHandlingScan = true;
        try
        {
            var code = scanCode;
            if (string.IsNullOrWhiteSpace(code)) return;

            try
            {
                var resourceCode = code.Trim();
                var tasks = await _qualityApi.GetProductionQualityOrdersByResourceAsync(resourceCode);
                MachineQualityTaskStore.Save(resourceCode, tasks);
                await Shell.Current.GoToAsync($"{AppShell.RouteMachineQualityTasks}?resourceCode={Uri.EscapeDataString(resourceCode)}");
            }
            catch (Exception ex)
            {
                await ErrorDialogService.ShowAsync(this, "查询失败", ex.Message, "确定");
            }
        }
        finally
        {
            _isHandlingScan = false;
        }
    }
}
