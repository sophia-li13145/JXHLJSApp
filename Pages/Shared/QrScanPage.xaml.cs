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
    private bool _wedgeFocusHooked;
    private bool _isPickingFromGallery;

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
        if (_returned) return;

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
                barcodeView.IsDetecting = true;
            }
        });
    }

    private async void PickFromGalleryButton_Clicked(object? sender, EventArgs e)
    {
        if (_returned || _isPickingFromGallery) return;

        _isPickingFromGallery = true;

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
                return;
            }

            await using var stream = await pick.OpenReadAsync();
            using var skBitmap = SKBitmap.Decode(stream);
            if (skBitmap is null)
            {
                await DisplayAlert("提示", "无法读取该图片。", "确定");
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
                return;
            }

            await HandleScanResultAsync(result.Text.Trim());
        }
        catch (Exception ex)
        {
            await DisplayAlert("错误", $"识别失败：{ex.Message}", "确定");
        }
        finally
        {
            _isPickingFromGallery = false;

            if (!_returned)
            {
                try { barcodeView.IsDetecting = true; } catch { }
#if ANDROID
                EnsureWedgeFocus();
#endif
            }
        }
    }

    private ZXing.Result? DecodeWithZxing(SKBitmap bitmap, bool tryHarder, bool tryInverted)
    {
        var options = new ZXing.Common.DecodingOptions
        {
            TryHarder = tryHarder,
            TryInverted = tryInverted,
            PossibleFormats = new[]
            {
                BarcodeFormat.CODE_128,
                BarcodeFormat.CODE_39,
                BarcodeFormat.EAN_13,
                BarcodeFormat.EAN_8,
                BarcodeFormat.ITF,
                BarcodeFormat.UPC_A,
                BarcodeFormat.UPC_E,
                BarcodeFormat.QR_CODE,
                BarcodeFormat.DATA_MATRIX,
                BarcodeFormat.PDF_417,
                BarcodeFormat.AZTEC
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
        var result = DecodeWithZxing(bitmap, tryHarder: false, tryInverted: false);
        if (result is not null) return result;

        result = DecodeWithZxing(bitmap, tryHarder: true, tryInverted: false);
        if (result is not null) return result;

        using var grayscale = ToGrayscale(bitmap);
        result = DecodeWithZxing(grayscale, tryHarder: true, tryInverted: true);
        if (result is not null) return result;

        using var scaled = ResizeIfNeeded(grayscale, 1600);
        return DecodeWithZxing(scaled, tryHarder: true, tryInverted: true);
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

    private static SKBitmap ResizeIfNeeded(SKBitmap source, int maxEdge)
    {
        var width = source.Width;
        var height = source.Height;
        var max = Math.Max(width, height);
        if (max <= maxEdge) return source.Copy();

        var scale = maxEdge / (float)max;
        var newW = Math.Max(1, (int)Math.Round(width * scale));
        var newH = Math.Max(1, (int)Math.Round(height * scale));

        var resized = new SKBitmap(newW, newH, source.ColorType, source.AlphaType);
        source.ScalePixels(resized, SKFilterQuality.Medium);
        return resized;
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

        ModeHintLabel.Text = "支持相机与硬件扫码：可使用相机或扫描头进行识别。";
        ModeHintLabel.IsVisible = true;
        WedgeInputEntry.IsVisible = true;
        EnsureWedgeFocus();
        if (!_wedgeFocusHooked)
        {
            WedgeInputEntry.Unfocused += WedgeInputEntry_Unfocused;
            _wedgeFocusHooked = true;
        }
#else
        ModeHintLabel.IsVisible = false;
        WedgeInputEntry.IsVisible = false;
#endif

        CameraActionGrid.IsVisible = true;
        barcodeView.IsVisible = true;

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

        if (_wedgeFocusHooked)
        {
            WedgeInputEntry.Unfocused -= WedgeInputEntry_Unfocused;
            _wedgeFocusHooked = false;
        }
#endif

        if (barcodeView != null)
        {
            barcodeView.IsDetecting = false;
        }
    }

#if ANDROID
    private void WedgeInputEntry_Unfocused(object? sender, FocusEventArgs e)
    {
        if (!_returned && !_isPickingFromGallery)
        {
            EnsureWedgeFocus();
        }
    }

    private void EnsureWedgeFocus()
    {
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                WedgeInputEntry.Focus();
                await Task.Delay(80);
                if (!WedgeInputEntry.IsFocused)
                {
                    WedgeInputEntry.Focus();
                }
            }
            catch
            {
                // 忽略焦点争抢导致的异常，避免影响扫码流程
            }
        });
    }

    private void OnHardwareScanned(string data, string? type)
    {
        if (string.IsNullOrWhiteSpace(data)) return;
        _ = HandleScanResultAsync(data.Trim());
    }
#endif
}
