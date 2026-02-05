using ZXing.Net.Maui;                     
using SkiaSharp;
using BarcodeFormat = ZXing.BarcodeFormat;                       

namespace JXHLJSApp.Pages;

public partial class QrScanPage : ContentPage
{
    private readonly TaskCompletionSource<string> _tcs;
    private bool _returned;
    /// <summary>执行 QrScanPage 初始化逻辑。</summary>
    public QrScanPage(TaskCompletionSource<string> tcs)
    {
        InitializeComponent();
        _tcs = tcs;

        // 直接在这里设置一次就够了
        barcodeView.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.OneDimensional | BarcodeFormats.TwoDimensional,
            AutoRotate = true,
            Multiple = false,
            TryHarder = false,
            TryInverted = false
        };

        // 降低实时预览帧压力，提升连续扫码响应速度
        barcodeView.DelayBetweenAnalyzingFrames = 60;
        barcodeView.DelayBetweenContinuousScans = 800;
    }


    // 扫码事件
    /// <summary>执行 BarcodesDetected 逻辑。</summary>
    private void BarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        if (_returned) return; // 防止重复触发

        var first = e.Results.FirstOrDefault();
        if (first == null || string.IsNullOrWhiteSpace(first.Value))
            return;

        _returned = true;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try { barcodeView.IsDetecting = false; } catch { }
            _tcs.TrySetResult(first.Value.Trim());
            await Navigation.PopAsync();
        });
    }

    // 新增：从相册选择图片并识别
    /// <summary>执行 PickFromGalleryButton_Clicked 逻辑。</summary>
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
                try { barcodeView.IsDetecting = true; } catch { }
                return;
            }

            await using var stream = await pick.OpenReadAsync();
            using var skBitmap = SKBitmap.Decode(stream);
            if (skBitmap is null)
            {
                await DisplayAlert("提示", "无法读取该图片。", "确定");
                try { barcodeView.IsDetecting = true; } catch { }
                return;
            }

            // 原图尺寸
            var w0 = skBitmap.Width;
            var h0 = skBitmap.Height;

            var result = DecodeWithFallbacks(skBitmap);

            if (result is null || string.IsNullOrWhiteSpace(result.Text))
            {
                await DisplayAlert(
                    "提示",
                    $"未识别到条码。\n原图: {w0}x{h0}",
                    "确定");
                try { barcodeView.IsDetecting = true; } catch { }
                return;
            }

            if (_returned) return;
            _returned = true;

            _tcs.TrySetResult(result.Text.Trim());
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("错误", $"识别失败：{ex.Message}", "确定");
            try { barcodeView.IsDetecting = true; } catch { }
        }
    }

    /// <summary>
    /// 统一的 ZXing 解码逻辑（原图和放大图都走它）
    /// </summary>
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

        // 这里不再裁剪，先用整图识别，成功率反而更高
        return reader.Decode(bitmap);
    }

    private ZXing.Result? DecodeWithFallbacks(SKBitmap bitmap)
    {
        // 优先走原图，命中率最高且速度最快
        var result = DecodeWithZxing(bitmap);
        if (result is not null) return result;

        // 仅保留一次灰度兜底，避免多轮高成本重采样导致等待过长
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

    // 前后摄像头切换
    /// <summary>执行 SwitchCameraButton_Clicked 逻辑。</summary>
    private void SwitchCameraButton_Clicked(object sender, EventArgs e)
    {
        barcodeView.CameraLocation =
            barcodeView.CameraLocation == CameraLocation.Rear
            ? CameraLocation.Front
            : CameraLocation.Rear;
    }

    // 手电筒开关
    /// <summary>执行 TorchButton_Clicked 逻辑。</summary>
    private void TorchButton_Clicked(object sender, EventArgs e)
    {
        barcodeView.IsTorchOn = !barcodeView.IsTorchOn;
    }

    /// <summary>执行 OnAppearing 逻辑。</summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // ✅ 动态请求相机权限（防止直接闪退）
        var status = await Permissions.RequestAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            await DisplayAlert("提示", "未授予相机权限，无法使用扫码功能。", "确定");
            await Navigation.PopAsync();
            return;
        }
        barcodeView.IsDetecting = true;
    }

    /// <summary>执行 OnDisappearing 逻辑。</summary>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // ✅ 防御性判断，防止闪退
        if (barcodeView != null)
        {
            barcodeView.IsDetecting = false;
        }
    }

}
