using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace JXHLJSApp.Services;

public interface IScanService
{
    Task<string?> ScanAsync(string title = "扫码", CancellationToken ct = default);
}

public sealed class ScanService : IScanService
{
    public async Task<string?> ScanAsync(string title = "扫码", CancellationToken ct = default)
    {
        var navigation = Shell.Current?.Navigation ?? Application.Current?.MainPage?.Navigation;
        if (navigation is null) return null;

        var permission = await Permissions.RequestAsync<Permissions.Camera>();
        var scannerPage = new ScannerModalPage(title, permission == PermissionStatus.Granted);
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

    private sealed class ScannerModalPage : ContentPage
    {
        private readonly TaskCompletionSource<string?> _resultSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CameraBarcodeReaderView _cameraView;
        private readonly Entry _hardwareScanEntry;
        private bool _completed;

        public ScannerModalPage(string title, bool enableCamera)
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
                    CreateHardwareInputPanel()
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

        private View CreateHardwareInputPanel()
        {
            var panel = new VerticalStackLayout
            {
                Padding = new Thickness(22, 12, 22, 26),
                Spacing = 10,
                Children =
                {
                    new Label { Text = "兼容安卓手机摄像头扫码和手持机扫码枪输入", TextColor = Color.FromArgb("#DDE8FF"), FontSize = 13, HorizontalTextAlignment = TextAlignment.Center },
                    _hardwareScanEntry
                }
            };
            Grid.SetRow(panel, 2);
            return panel;
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
