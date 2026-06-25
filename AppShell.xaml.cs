using JXHLJSApp.Pages;

namespace JXHLJSApp;

public partial class AppShell : Shell
{
    private readonly IServiceProvider _services;
    public const string RouteLogin = "//Login";

    public AppShell(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
        BuildLoginShell();
    }

    public void ApplyAuth(bool authed)
    {
        // The app currently only restores the login entry point; feature pages can be added later.
        MainThread.BeginInvokeOnMainThread(async () => await GoToAsync(RouteLogin));
    }

    private void BuildLoginShell()
    {
        Items.Clear();

        var tabBar = new TabBar();
        tabBar.Items.Add(new ShellContent
        {
            Route = "Login",
            ContentTemplate = new DataTemplate(() => _services.GetRequiredService<LoginPage>())
        });

        Items.Add(tabBar);
    }
}
