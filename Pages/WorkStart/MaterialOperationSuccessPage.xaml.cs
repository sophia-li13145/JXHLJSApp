namespace JXHLJSApp.Pages.WorkStart;

public partial class MaterialOperationSuccessPage : ContentPage
{
    public MaterialOperationSuccessPage()
    {
        InitializeComponent();
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await GoBackToExecutionAsync();

    private async void OnDoneClicked(object sender, EventArgs e) => await GoBackToExecutionAsync();

    private static Task GoBackToExecutionAsync() => Shell.Current.GoToAsync(AppShell.RouteWorkExecution);
}
