using JXHLJSApp.Pages;

namespace JXHLJSApp;

public partial class AppShell : Shell
{
    private readonly IServiceProvider _services;
    public const string RouteLogin = "//Login";
    public const string RouteHome = "//Home";
    public const string RouteAdmin = "Admin";
    public const string RouteLog = "Log";

    public AppShell(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
        Routing.RegisterRoute(RouteAdmin, typeof(AdminPage));
        Routing.RegisterRoute(RouteLog, typeof(LogPage));
        BuildLoginShell();
    }

    public void ApplyAuth(bool authed)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (authed)
            {
                BuildHomeShell();
                return;
            }

            BuildLoginShell();
        });
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

    private void BuildHomeShell()
    {
        Items.Clear();

        var tabBar = new TabBar();
        tabBar.Items.Add(new ShellContent
        {
            Route = "Home",
            ContentTemplate = new DataTemplate(() => _services.GetRequiredService<RoleHomePage>())
        });

        Items.Add(tabBar);
    }
}
