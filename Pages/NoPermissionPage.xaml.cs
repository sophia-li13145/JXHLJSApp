namespace JXHLJSApp.Pages;

public partial class NoPermissionPage : ContentPage
{
    public NoPermissionPage()
    {
        InitializeComponent();
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        await TokenStorage.ClearAsync();
        UserSessionStore.Clear();
        App.SwitchToLoggedOutShell();
    }
}
