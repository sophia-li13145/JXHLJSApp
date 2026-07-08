using JXHLJSApp.Pages;
using JXHLJSApp.Pages.WorkOrders;
using JXHLJSApp.Pages.WorkStart;
using JXHLJSApp.Pages.Warehouse;
using JXHLJSApp.Pages.Transport;

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
    public const string RouteRawMaterialReceiving = "RawMaterialReceiving";
    public const string RouteAddRawMaterialReceiving = "AddRawMaterialReceiving";
    public const string RouteRawMaterialReceivingDetail = "RawMaterialReceivingDetail";
    public const string RouteDeliveryOrders = "DeliveryOrders";
    public const string RouteDeliveryOrderDetail = "DeliveryOrderDetail";
    public const string RouteDeliveryCompletionSuccess = "DeliveryCompletionSuccess";
    public const string RoutePackagingSubTasks = "PackagingSubTasks";
    public const string RoutePackagingSubTaskDetail = "PackagingSubTaskDetail";
    public const string RouteProcessTransferScan = "ProcessTransferScan";
    public const string RouteOutstockTransportOrders = "OutstockTransportOrders";
    public const string RouteOutstockTransportOrderDetail = "OutstockTransportOrderDetail";
    public const string RouteProcessTransferConfirm = "ProcessTransferConfirm";
    public const string RouteProcessTransferSuccess = "ProcessTransferSuccess";

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
        Routing.RegisterRoute(RouteRawMaterialReceiving, typeof(RawMaterialReceivingListPage));
        Routing.RegisterRoute(RouteAddRawMaterialReceiving, typeof(AddRawMaterialReceivingPage));
        Routing.RegisterRoute(RouteRawMaterialReceivingDetail, typeof(RawMaterialReceivingDetailPage));
        Routing.RegisterRoute(RouteDeliveryOrders, typeof(DeliveryOrderListPage));
        Routing.RegisterRoute(RouteDeliveryOrderDetail, typeof(DeliveryOrderDetailPage));
        Routing.RegisterRoute(RouteDeliveryCompletionSuccess, typeof(DeliveryCompletionSuccessPage));
        Routing.RegisterRoute(RoutePackagingSubTasks, typeof(PackagingSubTaskListPage));
        Routing.RegisterRoute(RoutePackagingSubTaskDetail, typeof(PackagingSubTaskDetailPage));
        Routing.RegisterRoute(RouteProcessTransferScan, typeof(ProcessTransferScanPage));
        Routing.RegisterRoute(RouteOutstockTransportOrders, typeof(OutstockTransportOrderListPage));
        Routing.RegisterRoute(RouteOutstockTransportOrderDetail, typeof(OutstockTransportOrderDetailPage));
        Routing.RegisterRoute(RouteProcessTransferConfirm, typeof(ProcessTransferConfirmPage));
        Routing.RegisterRoute(RouteProcessTransferSuccess, typeof(ProcessTransferSuccessPage));
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
