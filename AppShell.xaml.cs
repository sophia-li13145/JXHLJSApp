using JXHLJSApp.Pages;
using JXHLJSApp.Pages.WorkOrders;
using JXHLJSApp.Pages.WorkStart;

namespace JXHLJSApp;

public partial class AppShell : Shell
{
    private readonly IServiceProvider _services;
    public const string RouteLogin = "//Login";
    public const string RouteHome = "//Home";
    public const string RouteAdmin = "Admin";
    public const string RouteLog = "Log";
    public const string RouteWorkOrderTasks = "WorkOrderTasks";
    public const string RouteWorkStartScan = "WorkStartScan";
    public const string RouteWorkStartOrders = "WorkStartOrders";
    public const string RouteWorkOrderInstruction = "WorkOrderInstruction";
    public const string RouteWorkExecution = "WorkExecution";
    public const string RouteMaterialLoading = "MaterialLoading";

    public AppShell(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
        Routing.RegisterRoute(RouteAdmin, typeof(AdminPage));
        Routing.RegisterRoute(RouteLog, typeof(LogPage));
        Routing.RegisterRoute(RouteWorkOrderTasks, typeof(WorkOrderTaskListPage));
        Routing.RegisterRoute(RouteWorkStartScan, typeof(WorkStartScanPage));
        Routing.RegisterRoute(RouteWorkStartOrders, typeof(WorkStartOrdersPage));
        Routing.RegisterRoute(RouteWorkOrderInstruction, typeof(WorkOrderInstructionPage));
        Routing.RegisterRoute(RouteWorkExecution, typeof(WorkExecutionPage));
        Routing.RegisterRoute(RouteMaterialLoading, typeof(MaterialLoadingPage));
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
