using JXHLJSApp.Services;
using JXHLJSApp.Services.Quality;

namespace JXHLJSApp.Pages.Quality;

public partial class MachineQualityScanPage : ContentPage
{
    private readonly IScanService _scanService;
    private readonly IQualityApi _qualityApi;

    public MachineQualityScanPage(IScanService scanService, IQualityApi qualityApi)
    {
        InitializeComponent();
        _scanService = scanService;
        _qualityApi = qualityApi;
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnScanTapped(object sender, TappedEventArgs e)
    {
        var code = await _scanService.ScanAsync("扫描机台二维码");
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
}
