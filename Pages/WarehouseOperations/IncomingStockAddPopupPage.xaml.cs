using JXHLJSApp.ViewModels;

namespace JXHLJSApp.Pages;

public partial class IncomingStockAddPopupPage : ContentPage
{
    private readonly IncomingStockAddPopupViewModel _vm;

    /// <summary>执行 IncomingStockAddPopupPage 初始化逻辑。</summary>
    public IncomingStockAddPopupPage(IncomingStockAddPopupViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    public static async Task<Models.IncomingBarcodeParseResult?> ShowAsync(
        IServiceProvider? sp,
        string? presetBarcode = null)
    {
        var tcs = new TaskCompletionSource<Models.IncomingBarcodeParseResult?>();
        var provider = sp ?? Application.Current?.Handler?.MauiContext?.Services;

        IncomingStockAddPopupViewModel vm =
            provider is not null
                ? ActivatorUtilities.CreateInstance<IncomingStockAddPopupViewModel>(provider)
                : throw new InvalidOperationException("无法解析服务依赖。");

        vm.SetResultTcs(tcs);

        var page = new IncomingStockAddPopupPage(vm);
        if (Application.Current?.MainPage?.Navigation is not null)
            await Application.Current.MainPage.Navigation.PushModalAsync(page);

        if (!string.IsNullOrWhiteSpace(presetBarcode))
        {
            await vm.LoadByBarcodeAsync(presetBarcode);
        }

        return await tcs.Task;
    }

    private async void OnBarcodeCompleted(object sender, EventArgs e)
    {
        await _vm.LoadByBarcodeAsync(_vm.Barcode);
    }

    private async void OnScanClicked(object sender, EventArgs e)
    {
        var tcs = new TaskCompletionSource<string>();
        await Navigation.PushAsync(new QrScanPage(tcs));

        var result = await tcs.Task;
        if (string.IsNullOrWhiteSpace(result)) return;

        await _vm.LoadByBarcodeAsync(result.Trim());
    }
}
