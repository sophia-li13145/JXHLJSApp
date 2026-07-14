namespace JXHLJSApp.Pages.Warehouse;

public sealed class TicketPhotoCapturePage : ContentPage
{
    private readonly TaskCompletionSource<FileResult?> _completion = new();
    private bool _openedCamera;
    private bool _isPicking;

    public TicketPhotoCapturePage()
    {
        Shell.SetNavBarIsVisible(this, false);
        BackgroundColor = Colors.Black;
        Padding = 0;

        var previewHint = new VerticalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = "📷",
                    FontSize = 48,
                    HorizontalTextAlignment = TextAlignment.Center,
                    TextColor = Colors.White
                },
                new Label
                {
                    Text = "正在打开相机，请对准票签拍照",
                    FontSize = 16,
                    HorizontalTextAlignment = TextAlignment.Center,
                    TextColor = Colors.White
                },
                new Label
                {
                    Text = "也可以点击左下角从相册选择",
                    FontSize = 13,
                    HorizontalTextAlignment = TextAlignment.Center,
                    TextColor = Color.FromArgb("#D6E4FF")
                }
            }
        };

        var albumButton = CreateBottomButton("🖼", "相册");
        albumButton.Clicked += OnAlbumClicked;

        var cameraButton = CreateBottomButton("📷", "拍照");
        cameraButton.Clicked += OnCameraClicked;

        var cancelButton = CreateBottomButton("✕", "取消");
        cancelButton.Clicked += async (_, _) => await CloseWithResultAsync(null);

        var bottomBar = new Grid
        {
            Padding = new Thickness(24, 14, 24, 28),
            BackgroundColor = Color.FromArgb("#1F1F1F"),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            }
        };
        bottomBar.Add(albumButton, 0, 0);
        bottomBar.Add(cameraButton, 1, 0);
        bottomBar.Add(cancelButton, 2, 0);

        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            Children =
            {
                previewHint,
                bottomBar
            }
        };
        Grid.SetRow(bottomBar, 1);
    }

    public Task<FileResult?> Completion => _completion.Task;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_openedCamera) return;

        _openedCamera = true;
        await CapturePhotoAsync();
    }

    private static Button CreateBottomButton(string icon, string text)
    {
        return new Button
        {
            Text = $"{icon}\n{text}",
            BackgroundColor = Colors.Transparent,
            TextColor = Colors.White,
            FontSize = 14,
            CornerRadius = 8,
            Padding = new Thickness(8),
            HeightRequest = 76
        };
    }

    private async void OnCameraClicked(object? sender, EventArgs e)
    {
        await CapturePhotoAsync();
    }

    private async void OnAlbumClicked(object? sender, EventArgs e)
    {
        if (_isPicking) return;

        try
        {
            _isPicking = true;
            var photo = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions { Title = "选择票签图片" });
            if (photo is not null)
            {
                await CloseWithResultAsync(photo);
            }
        }
        catch (Exception ex) when (ex is FeatureNotSupportedException || ex is FeatureNotEnabledException || ex is PermissionException)
        {
            await DisplayAlert("提示", "当前设备无法从相册选择图片。", "确定");
        }
        finally
        {
            _isPicking = false;
        }
    }

    private async Task CapturePhotoAsync()
    {
        if (_isPicking) return;

        try
        {
            _isPicking = true;
            var permission = await Permissions.RequestAsync<Permissions.Camera>();
            if (permission != PermissionStatus.Granted)
            {
                await DisplayAlert("提示", "未授予摄像头权限，可点击左下角从相册选择票签图片。", "确定");
                return;
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions { Title = "票签拍照" });
            if (photo is not null)
            {
                await CloseWithResultAsync(photo);
            }
        }
        catch (Exception ex) when (ex is FeatureNotSupportedException || ex is FeatureNotEnabledException || ex is PermissionException)
        {
            await DisplayAlert("提示", "当前设备无法直接调用相机，可点击左下角从相册选择票签图片。", "确定");
        }
        finally
        {
            _isPicking = false;
        }
    }

    private async Task CloseWithResultAsync(FileResult? result)
    {
        if (!_completion.Task.IsCompleted)
        {
            _completion.SetResult(result);
        }

        if (Navigation.ModalStack.Contains(this))
        {
            await Navigation.PopModalAsync();
        }
    }
}
