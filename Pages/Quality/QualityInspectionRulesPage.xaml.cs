namespace JXHLJSApp.Pages.Quality;

public partial class QualityInspectionRulesPage : ContentPage
{
    private readonly INavigation _ownerNavigation;
    private readonly TaskCompletionSource _closed = new();
    private bool _isClosing;

    public QualityInspectionRulesPage(INavigation ownerNavigation)
    {
        InitializeComponent();
        _ownerNavigation = ownerNavigation;
    }

    public Task WaitForCloseAsync() => _closed.Task;

    private void OnPageSizeChanged(object sender, EventArgs e)
    {
        if (Width <= 40 || Height <= 48) return;
        RulesCard.WidthRequest = Math.Min(Width - 40, 420);
        RulesCard.HeightRequest = Math.Min(Height - 48, 640);
    }

    private async void OnCloseClicked(object sender, EventArgs e) => await CloseAsync();

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync();
        return true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _closed.TrySetResult();
    }

    private async Task CloseAsync()
    {
        if (_isClosing) return;
        _isClosing = true;
        try
        {
            if (ReferenceEquals(_ownerNavigation.ModalStack.LastOrDefault(), this))
            {
                await _ownerNavigation.PopModalAsync(false);
            }
        }
        finally
        {
            _isClosing = false;
        }
    }
}
