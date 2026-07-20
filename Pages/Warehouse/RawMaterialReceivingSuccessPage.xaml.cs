namespace JXHLJSApp.Pages.Warehouse;

public partial class RawMaterialReceivingSuccessPage : ContentPage
{
    public RawMaterialReceivingSuccessPage()
    {
        InitializeComponent();
    }

    private async void OnReturnTapped(object sender, TappedEventArgs e) => await ReturnToReceivingListAsync();

    private async void OnReturnClicked(object sender, EventArgs e) => await ReturnToReceivingListAsync();

    private static Task ReturnToReceivingListAsync() => Shell.Current.GoToAsync("../..");
}
