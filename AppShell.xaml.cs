using JXHLJSApp.Pages;
using JXHLJSApp.Pages.WorkOrders;
using JXHLJSApp.Pages.WorkStart;
using JXHLJSApp.Pages.Quality;
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
    public const string RouteProductionStatistics = "ProductionStatistics";
    public const string RouteWorkStartScan = "WorkStartScan";
    public const string RouteWorkStartOrders = "WorkStartOrders";
    public const string RouteWorkOrderInstruction = "WorkOrderInstruction";
    public const string RouteWorkExecution = "WorkExecution";
    public const string RouteMaterialLoading = "MaterialLoading";
    public const string RouteMaterialUnloading = "MaterialUnloading";
    public const string RouteMaterialOperationSuccess = "MaterialOperationSuccess";
    public const string RouteWorkCompletion = "WorkCompletion";
    public const string RouteAbnormalReport = "AbnormalReport";
    public const string RouteReworkReport = "ReworkReport";
    public const string RouteAbnormalReportSuccess = "AbnormalReportSuccess";
    public const string RouteRawMaterialReceiving = "RawMaterialReceiving";
    public const string RouteAddRawMaterialReceiving = "AddRawMaterialReceiving";
    public const string RouteRawMaterialReceivingDetail = "RawMaterialReceivingDetail";
    public const string RouteRawMaterialReceivingSuccess = "RawMaterialReceivingSuccess";
    public const string RouteDeliveryOrders = "DeliveryOrders";
    public const string RouteDeliveryOrderDetail = "DeliveryOrderDetail";
    public const string RouteDeliveryCompletionSuccess = "DeliveryCompletionSuccess";
    public const string RoutePackagingSubTasks = "PackagingSubTasks";
    public const string RoutePackagingSubTaskDetail = "PackagingSubTaskDetail";
    public const string RouteProcessTransferScan = "ProcessTransferScan";
    public const string RouteOutstockTransportOrders = "OutstockTransportOrders";
    public const string RouteOutstockTransportOrderDetail = "OutstockTransportOrderDetail";
    public const string RouteProductInstockTransportOrders = "ProductInstockTransportOrders";
    public const string RouteProductInstockTransportOrderDetail = "ProductInstockTransportOrderDetail";
    public const string RouteProcessTransferConfirm = "ProcessTransferConfirm";
    public const string RouteProcessTransferSuccess = "ProcessTransferSuccess";
    public const string RouteIncomingQualityOrders = "IncomingQualityOrders";
    public const string RouteIncomingQualityOrderDetail = "IncomingQualityOrderDetail";
    public const string RouteIncomingQualityScan = "IncomingQualityScan";
    public const string RouteProductionQualityOrders = "ProductionQualityOrders";
    public const string RouteMachineQualityScan = "MachineQualityScan";
    public const string RouteMachineQualityTasks = "MachineQualityTasks";
    public const string RouteMachineQualityDetail = "MachineQualityDetail";

    public AppShell(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
        Routing.RegisterRoute(RouteAdmin, typeof(AdminPage));
        Routing.RegisterRoute(RouteLog, typeof(LogPage));
        Routing.RegisterRoute(RouteWorkOrderTasks, typeof(WorkOrderTaskListPage));
        Routing.RegisterRoute(RouteProductionStatistics, typeof(ProductionStatisticsPage));
        Routing.RegisterRoute(RouteWorkStartScan, typeof(WorkStartScanPage));
        Routing.RegisterRoute(RouteWorkStartOrders, typeof(WorkStartOrdersPage));
        Routing.RegisterRoute(RouteWorkOrderInstruction, typeof(WorkOrderInstructionPage));
        Routing.RegisterRoute(RouteWorkExecution, typeof(WorkExecutionPage));
        Routing.RegisterRoute(RouteMaterialLoading, typeof(MaterialLoadingPage));
        Routing.RegisterRoute(RouteMaterialUnloading, typeof(MaterialUnloadingPage));
        Routing.RegisterRoute(RouteMaterialOperationSuccess, typeof(MaterialOperationSuccessPage));
        Routing.RegisterRoute(RouteWorkCompletion, typeof(WorkCompletionPage));
        Routing.RegisterRoute(RouteAbnormalReport, typeof(AbnormalReportPage));
        Routing.RegisterRoute(RouteReworkReport, typeof(ReworkReportPage));
        Routing.RegisterRoute(RouteAbnormalReportSuccess, typeof(AbnormalReportSuccessPage));
        Routing.RegisterRoute(RouteRawMaterialReceiving, typeof(RawMaterialReceivingListPage));
        Routing.RegisterRoute(RouteAddRawMaterialReceiving, typeof(AddRawMaterialReceivingPage));
        Routing.RegisterRoute(RouteRawMaterialReceivingDetail, typeof(RawMaterialReceivingDetailPage));
        Routing.RegisterRoute(RouteRawMaterialReceivingSuccess, typeof(RawMaterialReceivingSuccessPage));
        Routing.RegisterRoute(RouteDeliveryOrders, typeof(DeliveryOrderListPage));
        Routing.RegisterRoute(RouteDeliveryOrderDetail, typeof(DeliveryOrderDetailPage));
        Routing.RegisterRoute(RouteDeliveryCompletionSuccess, typeof(DeliveryCompletionSuccessPage));
        Routing.RegisterRoute(RoutePackagingSubTasks, typeof(PackagingSubTaskListPage));
        Routing.RegisterRoute(RoutePackagingSubTaskDetail, typeof(PackagingSubTaskDetailPage));
        Routing.RegisterRoute(RouteProcessTransferScan, typeof(ProcessTransferScanPage));
        Routing.RegisterRoute(RouteOutstockTransportOrders, typeof(OutstockTransportOrderListPage));
        Routing.RegisterRoute(RouteOutstockTransportOrderDetail, typeof(OutstockTransportOrderDetailPage));
        Routing.RegisterRoute(RouteProductInstockTransportOrders, typeof(ProductInstockTransportOrderListPage));
        Routing.RegisterRoute(RouteProductInstockTransportOrderDetail, typeof(ProductInstockTransportOrderDetailPage));
        Routing.RegisterRoute(RouteProcessTransferConfirm, typeof(ProcessTransferConfirmPage));
        Routing.RegisterRoute(RouteProcessTransferSuccess, typeof(ProcessTransferSuccessPage));
        Routing.RegisterRoute(RouteIncomingQualityOrders, typeof(IncomingQualityOrderListPage));
        Routing.RegisterRoute(RouteIncomingQualityOrderDetail, typeof(IncomingQualityOrderDetailPage));
        Routing.RegisterRoute(RouteIncomingQualityScan, typeof(IncomingQualityScanPage));
        Routing.RegisterRoute(RouteProductionQualityOrders, typeof(ProductionQualityOrderListPage));
        Routing.RegisterRoute(RouteMachineQualityScan, typeof(MachineQualityScanPage));
        Routing.RegisterRoute(RouteMachineQualityTasks, typeof(MachineQualityTaskListPage));
        Routing.RegisterRoute(RouteMachineQualityDetail, typeof(MachineQualityDetailPage));
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
