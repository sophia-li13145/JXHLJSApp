namespace JXHLJSApp.Pages.Warehouse;

public partial class DeliveryCompletionSuccessPage : ContentPage
{
    public DeliveryCompletionSuccessPage()
    {
        InitializeComponent();
    }

    private async void OnReturnTapped(object sender, TappedEventArgs e) => await ReturnToDeliveryOrdersAsync();

    private async void OnReturnClicked(object sender, EventArgs e) => await ReturnToDeliveryOrdersAsync();

    private static async Task ReturnToDeliveryOrdersAsync() => await Shell.Current.GoToAsync("../..");
}
