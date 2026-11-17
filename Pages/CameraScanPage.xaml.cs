using JXHLJSApp.Services;
using ZXing.Net.Maui;

namespace JXHLJSApp.Pages;

public partial class CameraScanPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _closing;

    public CameraScanPage(string title)
    {
        InitializeComponent();
        ScanTitle.Text = title;
        TorchButton.IsVisible = Platform.AppContext.PackageManager?.HasSystemFeature("android.hardware.camera.flash") == true;
        Reader.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormat.QrCode,
            AutoRotate = true,
            Multiple = false
        };
    }

    public Task<string?> Result => _result.Task;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Reader.IsDetecting = !_closing;
    }

    protected override void OnDisappearing()
    {
        Reader.IsDetecting = false;
        Reader.IsTorchOn = false;
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(null);
        return true;
    }

    private async void OnCancelClicked(object sender, EventArgs e) => await CloseAsync(null);

    private void OnTorchClicked(object sender, EventArgs e)
    {
        if (_closing) return;
        Reader.IsTorchOn = !Reader.IsTorchOn;
        TorchButton.Text = Reader.IsTorchOn ? "关闭手电筒" : "打开手电筒";
    }

    private void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        var code = e.Results.FirstOrDefault(result => !string.IsNullOrWhiteSpace(result.Value))?.Value;
        if (code is not null)
            Dispatcher.Dispatch(async () => await CloseAsync(code));
    }

    private async Task CloseAsync(string? code)
    {
        if (_closing || Navigation.ModalStack.LastOrDefault() != this) return;
        _closing = true;
        Reader.IsDetecting = false;
        try
        {
            await Navigation.PopModalAsync();
            _result.TrySetResult(code);
        }
        catch (Exception ex)
        {
            _closing = false;
            Reader.IsDetecting = true;
            await ErrorDialogService.ShowAsync(this, "关闭扫码失败", ex.Message);
        }
    }
}
