using JXHLJSApp.Controls;
using SkiaSharp;
using ZXing.Common;
using ZXing.Net.Maui;
using SkiaBarcodeReader = ZXing.SkiaSharp.BarcodeReader;

namespace JXHLJSApp.Services;

public interface IScanService
{
    Task<string?> ScanAsync(
        string title = "扫码",
        CancellationToken ct = default,
        BarcodeFormat? formats = null);
    Task<string?> ScanFromPhotoAsync(string title = "选择二维码图片", CancellationToken ct = default);
}

public sealed class ScanService : IScanService
{
    public async Task<string?> ScanAsync(
        string title = "扫码",
        CancellationToken ct = default,
        BarcodeFormat? formats = null)
    {
        var navigation = Shell.Current?.Navigation ?? Application.Current?.MainPage?.Navigation;
        if (navigation is null) return null;

        var permission = await Permissions.RequestAsync<Permissions.Camera>();
        var scannerPage = new ScannerModalPage(
            title,
            permission == PermissionStatus.Granted,
            formats ?? BarcodeFormats.All,
            () => ScanFromPhotoAsync("选择二维码图片", ct));
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
        private readonly FocusedCameraBarcodeReaderView _cameraView;
        private readonly Label _cameraStatusLabel;
        private readonly Entry _hardwareScanEntry;
        private readonly bool _enableCamera;
        private bool _completed;
        private bool _visible;
        private bool _pickingPhoto;

        public ScannerModalPage(
            string title,
            bool enableCamera,
            BarcodeFormat formats,
            Func<Task<string?>> pickPhotoAsync)
        {
            Title = title;
            BackgroundColor = Color.FromArgb("#0B1220");
            Shell.SetNavBarIsVisible(this, false);
            _enableCamera = enableCamera;

            _cameraStatusLabel = new Label
            {
                Text = "请将二维码对准扫码框，点击画面可重新对焦。",
                TextColor = Color.FromArgb("#DDE8FF"),
                FontSize = 13,
                HorizontalTextAlignment = TextAlignment.Center
            };
            _cameraView = new FocusedCameraBarcodeReaderView
            {
                CameraLocation = CameraLocation.Rear,
                IsDetecting = false,
                Options = new BarcodeReaderOptions
                {
                    Formats = formats,
                    AutoRotate = true,
                    Multiple = false,
                    TryHarder = true
                },
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            };
            var focusTap = new TapGestureRecognizer();
            focusTap.Tapped += (_, e) =>
            {
                if (_completed || !_visible || _pickingPhoto || !_enableCamera) return;
                if (e.GetPosition(_cameraView) is Point point)
                    ((ICameraView)_cameraView).Focus(point);
            };
            _cameraView.GestureRecognizers.Add(focusTap);

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
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _visible = true;
            // A modal dialog or photo picker can reactivate this same scanner instance.
            _cameraView.BarcodesDetected -= OnBarcodesDetected;
            _cameraView.BarcodesDetected += OnBarcodesDetected;
            _cameraView.CameraStatusChanged -= OnCameraStatusChanged;
            _cameraView.CameraStatusChanged += OnCameraStatusChanged;
            StartScanner();
        }

        private void StartScanner()
        {
            if (_completed || !_visible || _pickingPhoto) return;
            if (!_enableCamera)
            {
                _hardwareScanEntry.Focus();
                return;
            }

            _cameraView.IsEnabled = true;
            // The Android handler focuses when the preview actually starts streaming.
            _cameraView.IsDetecting = true;
        }

        protected override void OnDisappearing()
        {
            _visible = false;
            ReleaseCamera();
            _cameraView.BarcodesDetected -= OnBarcodesDetected;
            _cameraView.CameraStatusChanged -= OnCameraStatusChanged;
            base.OnDisappearing();
        }

        protected override bool OnBackButtonPressed()
        {
            Complete(null);
            return true;
        }

        private void OnCameraStatusChanged(object? sender, string message)
        {
            if (!_completed && _visible && !_pickingPhoto) _cameraStatusLabel.Text = message;
        }

        public Task<string?> WaitForResultAsync() => _resultSource.Task;

        public void Cancel()
        {
            if (MainThread.IsMainThread)
            {
                Complete(null);
                return;
            }

            MainThread.BeginInvokeOnMainThread(() => Complete(null));
        }

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

            var frame = new Grid
            {
                Margin = new Thickness(22, 8),
                BackgroundColor = Colors.Black
            };

            if (enableCamera)
            {
                frame.Add(_cameraView);
            }
            else
            {
                frame.Add(new Label
                {
                    Text = "未授予摄像头权限，仍可使用手持机扫码枪输入。",
                    TextColor = Colors.White,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center
                });
            }

            frame.Add(new Border
            {
                Stroke = Color.FromArgb("#6EA8FF"),
                StrokeThickness = 2,
                InputTransparent = true
            });

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
            panel.Add(_cameraStatusLabel, 0, 1);
            panel.Add(_hardwareScanEntry, 0, 2);
            Grid.SetRow(panel, 2);
            return panel;
        }

        private async Task PickPhotoAsync(Func<Task<string?>> pickPhotoAsync)
        {
            if (_completed || _pickingPhoto) return;

            _pickingPhoto = true;
            ReleaseCamera();

            try
            {
                var code = await pickPhotoAsync();
                if (_completed) return;
                if (!string.IsNullOrWhiteSpace(code))
                {
                    Complete(code);
                    return;
                }

                await DisplayAlert("提示", "未从所选图片中识别到二维码。", "确定");
            }
            catch (Exception ex)
            {
                if (!_completed)
                    await ErrorDialogService.ShowAsync(this, "图片识别失败", ex.Message, "确定");
            }
            finally
            {
                _pickingPhoto = false;
                if (!_completed)
                {
                    StartScanner();
                }
            }
        }

        private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
        {
            var value = e.Results?.FirstOrDefault(result => !string.IsNullOrWhiteSpace(result.Value))?.Value;
            if (string.IsNullOrWhiteSpace(value)) return;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_visible && !_pickingPhoto) Complete(value);
            });
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
            ReleaseCamera();
            _resultSource.TrySetResult(text);
        }

        private void ReleaseCamera()
        {
            _cameraView.IsDetecting = false;
            _cameraView.IsEnabled = false;
        }
    }
}
