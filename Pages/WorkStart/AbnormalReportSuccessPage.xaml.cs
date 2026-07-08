namespace JXHLJSApp.Pages.WorkStart;

public partial class AbnormalReportSuccessPage : ContentPage
{
    public AbnormalReportSuccessPage()
    {
        InitializeComponent();
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnDoneClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync(AppShell.RouteHome);
}
