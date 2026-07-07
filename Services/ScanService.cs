using SkiaSharp;
using ZXing.Common;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;
using SkiaBarcodeReader = ZXing.SkiaSharp.BarcodeReader;

namespace JXHLJSApp.Services;

public interface IScanService
{
    Task<string?> ScanAsync(string title = "扫码", CancellationToken ct = default);
    Task<string?> ScanFromPhotoAsync(string title = "选择二维码图片", CancellationToken ct = default);
}

public sealed class ScanService : IScanService
{
    public async Task<string?> ScanAsync(string title = "扫码", CancellationToken ct = default)
    {
        var navigation = Shell.Current?.Navigation ?? Application.Current?.MainPage?.Navigation;
        if (navigation is null) return null;

        var permission = await Permissions.RequestAsync<Permissions.Camera>();
        var scannerPage = new ScannerModalPage(title, permission == PermissionStatus.Granted, () => ScanFromPhotoAsync("选择二维码图片", ct));
        await navigation.PushModalAsync(scannerPage);

        using var registration = ct.Register(() => scannerPage.Cancel());
        var result = await scannerPage.WaitForResultAsync().ConfigureAwait(false);

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (navigation.ModalStack.Contains(scannerPage))
            {
                await navigation.PopModalAsync();
            }
        });

        return result;
    }


    public async Task<string?> ScanFromPhotoAsync(string title = "选择二维码图片", CancellationToken ct = default)
    {
        var photo = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions { Title = title }).ConfigureAwait(false);
        if (photo is null) return null;

        await using var stream = await photo.OpenReadAsync().ConfigureAwait(false);
        using var bitmap = SKBitmap.Decode(stream);
        if (bitmap is null)
        {
            throw new InvalidOperationException("无法读取所选图片。");
        }

        var reader = new SkiaBarcodeReader
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                TryHarder = true
            }
        };
        return reader.Decode(bitmap)?.Text?.Trim();
    }

    private sealed class ScannerModalPage : ContentPage
    {
        private readonly TaskCompletionSource<string?> _resultSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CameraBarcodeReaderView _cameraView;
        private readonly Entry _hardwareScanEntry;
        private bool _completed;

        public ScannerModalPage(string title, bool enableCamera, Func<Task<string?>> pickPhotoAsync)
        {
            Title = title;
            BackgroundColor = Color.FromArgb("#0B1220");
            Shell.SetNavBarIsVisible(this, false);

            _cameraView = new CameraBarcodeReaderView
            {
                IsDetecting = enableCamera,
                Options = new BarcodeReaderOptions
                {
                    Formats = BarcodeFormats.All,
                    AutoRotate = true,
                    Multiple = false,
                    TryHarder = true
                },
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            };
            _cameraView.BarcodesDetected += OnBarcodesDetected;

            _hardwareScanEntry = new Entry
            {
                Placeholder = "手持机可直接按扫描键，或手动输入后回车",
                PlaceholderColor = Color.FromArgb("#B7C4D8"),
                TextColor = Colors.White,
                BackgroundColor = Color.FromArgb("#22345A"),
                ReturnType = ReturnType.Done,
                FontSize = 15
            };
            _hardwareScanEntry.Completed += (_, _) => Complete(_hardwareScanEntry.Text);
            _hardwareScanEntry.TextChanged += (_, e) =>
            {
                if (e.NewTextValue?.Contains('\n') == true || e.NewTextValue?.Contains('\r') == true)
                {
                    Complete(e.NewTextValue.Replace("\r", string.Empty).Replace("\n", string.Empty));
                }
            };

            Content = new Grid
            {
                RowDefinitions = new RowDefinitionCollection(new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Star }, new RowDefinition { Height = GridLength.Auto }),
                Children =
                {
                    CreateHeader(title),
                    CreateCameraFrame(enableCamera),
                    CreateHardwareInputPanel(pickPhotoAsync)
                }
            };
            Grid.SetRow(_cameraView, 1);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _cameraView.IsDetecting = _cameraView.IsEnabled;
            _hardwareScanEntry.Focus();
        }

        protected override void OnDisappearing()
        {
            _cameraView.IsDetecting = false;
            _cameraView.BarcodesDetected -= OnBarcodesDetected;
            base.OnDisappearing();
        }

        public Task<string?> WaitForResultAsync() => _resultSource.Task;

        public void Cancel() => Complete(null);

        private View CreateHeader(string title)
        {
            var close = new Button
            {
                Text = "取消",
                BackgroundColor = Colors.Transparent,
                TextColor = Colors.White,
                FontSize = 15
            };
            close.Clicked += (_, _) => Complete(null);

            var header = new Grid
            {
                Padding = new Thickness(20, 18, 20, 12),
                ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition { Width = GridLength.Star }, new ColumnDefinition { Width = GridLength.Auto })
            };
            header.Add(new Label { Text = title, TextColor = Colors.White, FontSize = 20, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center });
            header.Add(close, 1);
            return header;
        }

        private View CreateCameraFrame(bool enableCamera)
        {
            _cameraView.IsEnabled = enableCamera;
            var frame = new Border
            {
                Stroke = Color.FromArgb("#6EA8FF"),
                StrokeThickness = 2,
                Margin = new Thickness(22, 8),
                Content = enableCamera
                    ? _cameraView
                    : new Label
                    {
                        Text = "未授予摄像头权限，仍可使用手持机扫码枪输入。",
                        TextColor = Colors.White,
                        HorizontalTextAlignment = TextAlignment.Center,
                        VerticalTextAlignment = TextAlignment.Center
                    }
            };
            Grid.SetRow(frame, 1);
            return frame;
        }

        private View CreateHardwareInputPanel(Func<Task<string?>> pickPhotoAsync)
        {
            var pickPhotoButton = new Button
            {
                Text = "从相册选择",
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#DDE8FF"),
                FontSize = 14,
                Padding = new Thickness(0),
                HorizontalOptions = LayoutOptions.Start
            };
            pickPhotoButton.Clicked += async (_, _) => await PickPhotoAsync(pickPhotoAsync);

            var panel = new Grid
            {
                Padding = new Thickness(22, 12, 22, 26),
                RowDefinitions = new RowDefinitionCollection(
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto }),
                RowSpacing = 10
            };
            panel.Add(pickPhotoButton, 0, 0);
            panel.Add(new Label { Text = "兼容安卓手机摄像头扫码和手持机扫码枪输入", TextColor = Color.FromArgb("#DDE8FF"), FontSize = 13, HorizontalTextAlignment = TextAlignment.Center }, 0, 1);
            panel.Add(_hardwareScanEntry, 0, 2);
            Grid.SetRow(panel, 2);
            return panel;
        }

        private async Task PickPhotoAsync(Func<Task<string?>> pickPhotoAsync)
        {
            if (_completed) return;

            var shouldResumeDetecting = _cameraView.IsDetecting;
            _cameraView.IsDetecting = false;

            try
            {
                var code = await pickPhotoAsync();
                if (!string.IsNullOrWhiteSpace(code))
                {
                    Complete(code);
                    return;
                }

                await DisplayAlert("提示", "未从所选图片中识别到二维码。", "确定");
            }
            catch (Exception ex)
            {
                await DisplayAlert("图片识别失败", ex.Message, "确定");
            }
            finally
            {
                if (!_completed)
                {
                    _cameraView.IsDetecting = shouldResumeDetecting;
                    _hardwareScanEntry.Focus();
                }
            }
        }

        private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
        {
            var value = e.Results?.FirstOrDefault()?.Value;
            MainThread.BeginInvokeOnMainThread(() => Complete(value));
        }

        private void Complete(string? value)
        {
            if (_completed) return;

            var text = value?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                text = null;
            }

            _completed = true;
            _cameraView.IsDetecting = false;
            _resultSource.TrySetResult(text);
        }
    }
}
