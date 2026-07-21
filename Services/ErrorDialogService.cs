using Microsoft.Maui.Controls.Shapes;

namespace JXHLJSApp.Services;

public static class ErrorDialogService
{
    public static Task ShowAsync(Page? owner, string title, string message, string buttonText = "确定")
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = owner ?? Application.Current?.MainPage;
            if (page is null)
            {
                return;
            }

            var dialogPage = new ErrorDialogPage(
                string.IsNullOrWhiteSpace(title) ? "操作失败" : title,
                string.IsNullOrWhiteSpace(message) ? "操作未成功，请稍后重试。" : message,
                string.IsNullOrWhiteSpace(buttonText) ? "确定" : buttonText);

            await page.Navigation.PushModalAsync(dialogPage, false);
            await dialogPage.WaitForCloseAsync();
        });
    }

    private sealed class ErrorDialogPage : ContentPage
    {
        private readonly TaskCompletionSource _closed = new();

        public ErrorDialogPage(string title, string message, string buttonText)
        {
            BackgroundColor = Colors.Transparent;
            NavigationPage.SetHasNavigationBar(this, false);

            var closeButton = new Button
            {
                Text = buttonText,
                BackgroundColor = Color.FromArgb("#1F447E"),
                TextColor = Colors.White,
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                CornerRadius = 10,
                HeightRequest = 52,
                Margin = new Thickness(0, 10, 0, 0)
            };
            closeButton.Clicked += async (_, _) => await CloseAsync();

            Content = new Grid
            {
                BackgroundColor = Color.FromArgb("#66001431"),
                Padding = new Thickness(24),
                Children =
                {
                    new Border
                    {
                        BackgroundColor = Colors.White,
                        Stroke = new SolidColorBrush(Color.FromArgb("#FFE0E3")),
                        StrokeThickness = 1,
                        Padding = new Thickness(22, 22, 22, 18),
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        MaximumWidthRequest = 340,
                        StrokeShape = new RoundRectangle { CornerRadius = 16 },
                        Shadow = new Shadow
                        {
                            Brush = new SolidColorBrush(Color.FromArgb("#22000000")),
                            Offset = new Point(0, 6),
                            Radius = 14,
                            Opacity = 0.45f
                        },
                        Content = new VerticalStackLayout
                        {
                            Spacing = 16,
                            Children =
                            {
                                new Border
                                {
                                    WidthRequest = 64,
                                    HeightRequest = 64,
                                    BackgroundColor = Color.FromArgb("#F64148"),
                                    StrokeThickness = 0,
                                    HorizontalOptions = LayoutOptions.Center,
                                    StrokeShape = new RoundRectangle { CornerRadius = 12 },
                                    Content = new Label
                                    {
                                        Text = "!",
                                        TextColor = Colors.White,
                                        FontSize = 42,
                                        FontAttributes = FontAttributes.Bold,
                                        HorizontalTextAlignment = TextAlignment.Center,
                                        VerticalTextAlignment = TextAlignment.Center
                                    }
                                },
                                new Label
                                {
                                    Text = title,
                                    TextColor = Color.FromArgb("#001431"),
                                    FontSize = 22,
                                    FontAttributes = FontAttributes.Bold,
                                    HorizontalTextAlignment = TextAlignment.Center
                                },
                                new Label
                                {
                                    Text = message,
                                    TextColor = Color.FromArgb("#4B6688"),
                                    FontSize = 15,
                                    LineBreakMode = LineBreakMode.WordWrap,
                                    HorizontalTextAlignment = TextAlignment.Center
                                },
                                closeButton
                            }
                        }
                    }
                }
            };
        }

        public Task WaitForCloseAsync() => _closed.Task;

        protected override bool OnBackButtonPressed()
        {
            _ = CloseAsync();
            return true;
        }

        private async Task CloseAsync()
        {
            if (_closed.Task.IsCompleted)
            {
                return;
            }

            if (Navigation.ModalStack.Contains(this))
            {
                await Navigation.PopModalAsync(false);
            }

            _closed.TrySetResult();
        }
    }
}
