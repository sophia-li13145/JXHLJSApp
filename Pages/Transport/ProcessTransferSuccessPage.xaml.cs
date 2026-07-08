namespace JXHLJSApp.Pages.Transport;

public partial class ProcessTransferSuccessPage : ContentPage
{
    public ProcessTransferSuccessPage()
    {
        InitializeComponent();
    }

    private async void OnReturnTapped(object sender, TappedEventArgs e) => await ReturnHomeAsync();
    private async void OnReturnClicked(object sender, EventArgs e) => await ReturnHomeAsync();

    private static async Task ReturnHomeAsync()
    {
        TransportOrderNavigationStore.Current = null;
        await Shell.Current.GoToAsync(AppShell.RouteHome);
    }
}
