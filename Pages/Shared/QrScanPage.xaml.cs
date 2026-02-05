using ZXing.Net.Maui;
using SkiaSharp;
using BarcodeFormat = ZXing.BarcodeFormat;
using JXHLJSApp.Services;

namespace JXHLJSApp.Pages;

public partial class QrScanPage : ContentPage
{
    private readonly TaskCompletionSource<string> _tcs;
    private readonly ScanService _scanService;
    private bool _returned;
    private DateTime _lastDetectedAt = DateTime.MinValue;
    private static readonly TimeSpan MinDetectInterval = TimeSpan.FromMilliseconds(60);
    private int _handling = 0;
    private bool _hardwareScanPreferred;

    /// <summary>执行 QrScanPage 初始化逻辑。</summary>
    public QrScanPage(TaskCompletionSource<string> tcs)
    {
        InitializeComponent();
        _tcs = tcs;
        _scanService = ServiceHelper.GetService<ScanService>();

        barcodeView.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.OneDimensional | BarcodeFormats.TwoDimensional,
            AutoRotate = false,
            Multiple = false,
            TryHarder = false,
            TryInverted = false
        };

        _scanService.Attach(WedgeInputEntry);
    }

    private void BarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        if (_returned || _hardwareScanPreferred) return;

        if (System.Threading.Interlocked.Exchange(ref _handling, 1) == 1) return;

        try
        {
            var now = DateTime.UtcNow;
            if (now - _lastDetectedAt < MinDetectInterval) return;
            _lastDetectedAt = now;

            var first = e.Results?.FirstOrDefault();
            var value = first?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(value)) return;

            _ = HandleScanResultAsync(value);
        }
        finally
        {
            System.Threading.Interlocked.Exchange(ref _handling, 0);
        }
    }

    private async Task HandleScanResultAsync(string value)
    {
        if (_returned) return;

        _returned = true;
        barcodeView.IsDetecting = false;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                ResultLabel.Text = value;
                _tcs?.TrySetResult(value);
                await Navigation.PopAsync();
            }
            catch
            {
                _returned = false;
                if (!_hardwareScanPreferred)
                {
                    barcodeView.IsDetecting = true;
                }
            }
        });
    }

    private async void PickFromGalleryButton_Clicked(object? sender, EventArgs e)
    {
        try
        {
            try { barcodeView.IsDetecting = false; } catch { }

            var pick = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "选择包含二维码/条码的图片",
                FileTypes = FilePickerFileType.Images
            });

            if (pick is null)
            {
                try { if (!_hardwareScanPreferred) barcodeView.IsDetecting = true; } catch { }
                return;
            }

            await using var stream = await pick.OpenReadAsync();
            using var skBitmap = SKBitmap.Decode(stream);
            if (skBitmap is null)
            {
                await DisplayAlert("提示", "无法读取该图片。", "确定");
                try { if (!_hardwareScanPreferred) barcodeView.IsDetecting = true; } catch { }
                return;
            }

            var w0 = skBitmap.Width;
            var h0 = skBitmap.Height;

            var result = DecodeWithFallbacks(skBitmap);

            if (result is null || string.IsNullOrWhiteSpace(result.Text))
            {
                await DisplayAlert(
                    "提示",
                    $"未识别到条码。\n原图: {w0}x{h0}",
                    "确定");
                try { if (!_hardwareScanPreferred) barcodeView.IsDetecting = true; } catch { }
                return;
            }

            await HandleScanResultAsync(result.Text.Trim());
        }
        catch (Exception ex)
        {
            await DisplayAlert("错误", $"识别失败：{ex.Message}", "确定");
            try { if (!_hardwareScanPreferred) barcodeView.IsDetecting = true; } catch { }
        }
    }

    private ZXing.Result? DecodeWithZxing(SKBitmap bitmap)
    {
        var options = new ZXing.Common.DecodingOptions
        {
            TryHarder = false,
            TryInverted = false,
            PossibleFormats = new[]
            {
                BarcodeFormat.CODE_128,
                BarcodeFormat.CODE_39,
                BarcodeFormat.EAN_13,
                BarcodeFormat.EAN_8,
                BarcodeFormat.ITF,
                BarcodeFormat.UPC_A,
                BarcodeFormat.UPC_E,
                BarcodeFormat.QR_CODE
            }
        };

        var reader = new ZXing.SkiaSharp.BarcodeReader
        {
            AutoRotate = true,
            Options = options
        };

        return reader.Decode(bitmap);
    }

    private ZXing.Result? DecodeWithFallbacks(SKBitmap bitmap)
    {
        var result = DecodeWithZxing(bitmap);
        if (result is not null) return result;

        using var grayscale = ToGrayscale(bitmap);
        return DecodeWithZxing(grayscale);
    }

    private static SKBitmap ToGrayscale(SKBitmap source)
    {
        var grayscale = new SKBitmap(source.Width, source.Height, SKColorType.Bgra8888, source.AlphaType);
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var color = source.GetPixel(x, y);
                var lum = (byte)Math.Clamp((color.Red * 0.299f) + (color.Green * 0.587f) + (color.Blue * 0.114f), 0, 255);
                grayscale.SetPixel(x, y, new SKColor(lum, lum, lum, color.Alpha));
            }
        }

        return grayscale;
    }

    private void SwitchCameraButton_Clicked(object sender, EventArgs e)
    {
        barcodeView.CameraLocation =
            barcodeView.CameraLocation == CameraLocation.Rear
            ? CameraLocation.Front
            : CameraLocation.Rear;
    }

    private void TorchButton_Clicked(object sender, EventArgs e)
    {
        barcodeView.IsTorchOn = !barcodeView.IsTorchOn;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

#if ANDROID
        _scanService.Scanned += OnHardwareScanned;
        _scanService.StartListening();

        _hardwareScanPreferred = true;
        if (_hardwareScanPreferred)
        {
            ModeHintLabel.Text = "当前为手持机硬件扫码模式（Intent/Wedge），按扫描键即可。";
            ModeHintLabel.IsVisible = true;
            CameraActionGrid.IsVisible = false;
            barcodeView.IsVisible = false;
            WedgeInputEntry.IsVisible = true;
            WedgeInputEntry.Focus();
            ResultLabel.Text = "请使用扫描头扫码...";
            return;
        }
#endif

        ModeHintLabel.IsVisible = false;
        CameraActionGrid.IsVisible = true;
        barcodeView.IsVisible = true;
        WedgeInputEntry.IsVisible = false;

        var status = await Permissions.RequestAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            await DisplayAlert("提示", "未授予相机权限，无法使用扫码功能。", "确定");
            await Navigation.PopAsync();
            return;
        }

        barcodeView.IsDetecting = true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

#if ANDROID
        _scanService.Scanned -= OnHardwareScanned;
        _scanService.StopListening();
#endif

        if (barcodeView != null)
        {
            barcodeView.IsDetecting = false;
        }
    }

#if ANDROID
    private void OnHardwareScanned(string data, string? type)
    {
        if (string.IsNullOrWhiteSpace(data)) return;
        _ = HandleScanResultAsync(data.Trim());
    }
#endif
}
