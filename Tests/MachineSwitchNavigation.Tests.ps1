$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
function Read-Source($path) {
    [regex]::Replace((Get-Content (Join-Path $root $path) -Raw), '(?m)^using .*;\r?\n|^namespace .*;\r?\n', '')
}
$scanSource = Read-Source 'Pages/WorkStart/WorkStartScanPage.xaml.cs'
$ordersSource = Read-Source 'Pages/WorkStart/WorkStartOrdersPage.xaml.cs'
$contextSource = Read-Source 'Services/WorkOrders/ProductionContextService.cs'
$dtoSource = Read-Source 'Models/WorkOrders/WorkOrderTaskDto.cs'
$switchMethod = [regex]::Match((Get-Content (Join-Path $root 'Pages/WorkStart/WorkExecutionPage.xaml.cs') -Raw), '(?s)    private async void OnSwitchMachineTapped\(.*?(?=    private async void OnBackHomeTapped)').Value
$routeMethod = [regex]::Match((Get-Content (Join-Path $root 'AppShell.xaml.cs') -Raw), '    public static string ProductionOrderSelectionRoute\(string machineCode\) =>\s*[^;]+;').Value
$dedupMethod = [regex]::Match((Get-Content (Join-Path $root 'Services/WorkOrders/WorkOrderApi.cs') -Raw), '(?s)    private static List<WorkOrderTaskDto> DeduplicateCurrentUserMachineWorkOrders\(.*?(?=    public async Task<WorkOrderOperationResult> StartWorkOrderAsync)').Value.Replace('private static List<WorkOrderTaskDto>', 'public static List<WorkOrderTaskDto>')
if (!$switchMethod -or !$routeMethod -or !$dedupMethod) { throw 'Production navigation or deduplication method not found.' }
$doubles = @'
public class TappedEventArgs : EventArgs { }
public class NavigatedToEventArgs : EventArgs { }
public class NavigatedFromEventArgs : EventArgs { }
public interface IQueryAttributable { void ApplyQueryAttributes(IDictionary<string, object> query); }
public class ContentPage
{
    public string? Title { get; set; }
    public Navigation Navigation { get; } = new();
    protected virtual void OnNavigatedTo(NavigatedToEventArgs args) { }
    protected virtual void OnNavigatedFrom(NavigatedFromEventArgs args) { }
    protected virtual void OnAppearing() { }
    protected virtual void OnDisappearing() { }
    protected virtual bool OnBackButtonPressed() => false;
    public void Enter() { OnNavigatedTo(new()); OnAppearing(); }
    public void AppearOnly() => OnAppearing();
    public void NavigateToOnly() => OnNavigatedTo(new());
    public void Leave() { OnNavigatedFrom(new()); OnDisappearing(); }
    public Task DisplayAlert(string title, string message, string button) => Task.CompletedTask;
}
public class Navigation { public List<ContentPage> ModalStack { get; } = new(); }
public class Label { public string? Text { get; set; } }
public class Entry
{
    public string? Text { get; set; }
    public void Unfocus() { }
    public Task HideKeyboardAsync(CancellationToken ct) => Task.CompletedTask;
}
public class Button { public object? CommandParameter { get; set; } }
public class RefreshView { public bool IsRefreshing { get; set; } }
public class CollectionView { public object? ItemsSource { get; set; } public bool IsEnabled { get; set; } = true; }
public static class ErrorDialogService
{
    public static int Errors;
    public static bool IsDialogVisible { get; private set; }
    public static Task ShowAsync(ContentPage page, string title, string message, string button)
    {
        Errors++; IsDialogVisible = true;
        page.Navigation.ModalStack.Add(new ContentPage());
        page.Leave(); page.Navigation.ModalStack.Clear(); page.Enter();
        IsDialogVisible = false;
        return Task.CompletedTask;
    }
}
public static class Log { public static void Information(string message, params object?[] args) { } }
public interface IRightKeyScanService
{
    void Register(ContentPage page, Func<string, Task> handle, Func<bool>? ready = null);
    Task ShowHintAsync(ContentPage page, string hint);
}
public sealed class FakeScanner : IRightKeyScanService
{
    public Func<string, Task>? Handle;
    public void Register(ContentPage page, Func<string, Task> handle, Func<bool>? ready = null) => Handle = handle;
    public Task ShowHintAsync(ContentPage page, string hint) => Task.CompletedTask;
    public Task Scan(string code) => Handle!(code);
}
public sealed record WorkOrderOperationResult(bool success, string? message);
public interface IWorkOrderApi
{
    Task<bool> BindWorkerMachineAsync(string code, CancellationToken ct = default);
    Task<List<WorkOrderTaskDto>> GetCurrentUserMachinesWorkOrdersAsync(CancellationToken ct = default);
    Task<WorkOrderOperationResult> StartWorkOrderAsync(string number, CancellationToken ct = default);
}
public sealed class FakeApi : IWorkOrderApi
{
    public bool BindResult = true, StartResult = true, ThrowLoad;
    public int BindCalls, StartCalls, LoadCalls;
    public CancellationToken LastBindToken;
    public TaskCompletionSource<bool>? PendingBind;
    public TaskCompletionSource<WorkOrderOperationResult>? PendingStart;
    public TaskCompletionSource<List<WorkOrderTaskDto>>? PendingLoad;
    public List<WorkOrderTaskDto> Rows = new();
    public Task<bool> BindWorkerMachineAsync(string code, CancellationToken ct = default)
    {
        BindCalls++; LastBindToken = ct;
        return PendingBind is null ? Task.FromResult(BindResult) : PendingBind.Task.WaitAsync(ct);
    }
    public Task<List<WorkOrderTaskDto>> GetCurrentUserMachinesWorkOrdersAsync(CancellationToken ct = default)
    {
        LoadCalls++;
        if (ThrowLoad) throw new Exception("load failed");
        if (PendingLoad is { } pending) { PendingLoad = null; return pending.Task; }
        return Task.FromResult(ApiTools.DeduplicateCurrentUserMachineWorkOrders(Rows));
    }
    public Task<WorkOrderOperationResult> StartWorkOrderAsync(string number, CancellationToken ct = default)
    {
        StartCalls++;
        return PendingStart is null ? Task.FromResult(new WorkOrderOperationResult(StartResult, null)) : PendingStart.Task.WaitAsync(ct);
    }
}
public sealed class Shell
{
    public static Shell Current { get; set; } = new();
    public List<ContentPage> Stack { get; } = new();
    public ContentPage CurrentPage => Stack.Last();
    public Func<WorkStartScanPage>? CreateScanner;
    public Func<WorkStartOrdersPage>? CreateOrders;
    public bool FailNextSelection;
    public Task GoToAsync(string route)
    {
        if (route.StartsWith("//Home/RoleModule/WorkStartOrders") && FailNextSelection)
        { FailNextSelection = false; throw new Exception("navigation failed"); }
        CurrentPage.Leave();
        if (route == "..") Stack.RemoveAt(Stack.Count - 1);
        else if (route.StartsWith("//Home/RoleModule/WorkStartOrders"))
        {
            Stack.Clear(); Stack.Add(new ContentPage());
            var page = CreateOrders!();
            page.ApplyQueryAttributes(new Dictionary<string, object>
            {
                ["boundMachineCode"] = Uri.UnescapeDataString(route.Split("boundMachineCode=")[1]),
                ["refreshToken"] = route.Split("refreshToken=")[1].Split('&')[0]
            });
            Stack.Add(page);
        }
        else if (route.StartsWith(AppShell.RouteWorkStartScan))
        {
            var page = CreateScanner!();
            page.ApplyQueryAttributes(new Dictionary<string, object> { ["switchMachine"] = route.Contains("switchMachine=true") });
            Stack.Add(page);
        }
        else if (route == AppShell.RouteWorkExecution) Stack.Add(new WorkExecutionPage());
        else Stack.Add(new ContentPage());
        CurrentPage.Enter(); return Task.CompletedTask;
    }
}
public partial class WorkStartScanPage
{
    private void InitializeComponent() { }
    public Label PageTitleLabel = new(), ScanHintLabel = new();
    public Entry MachineNoEntry = new();
    public void TapRetry() => OnScanTapped(this, new());
    public bool PressBack() => OnBackButtonPressed();
}
public partial class WorkStartOrdersPage
{
    private void InitializeComponent() { }
    public CollectionView OrdersList = new();
    public RefreshView RefreshContainer = new();
    public Label HeaderLabel = new();
    public List<WorkOrderTaskDto> Rows => _orders;
    public bool Starting => _isStarting;
    public void Start(WorkOrderTaskDto order) => OnStartClicked(new Button { CommandParameter = order }, EventArgs.Empty);
    public void Refresh() => OnRefreshing(this, EventArgs.Empty);
    public void Rescan() => OnRescanTapped(this, new());
}
public partial class WorkExecutionPage : ContentPage
{
    private bool _isOpeningMachineScanner;
    public RefreshView RefreshContainer = new();
    public void ClickSwitch() => OnSwitchMachineTapped(this, new());
}
public static class Regression
{
    private static int _checks;
    private static void Check(bool value, string name) { _checks++; if (!value) throw new Exception(name); }
    private static void Wait(Func<bool> done)
    { if (!System.Threading.SpinWait.SpinUntil(done, 2000)) throw new Exception("Async completion timed out"); }
    private sealed class Fixture
    {
        public readonly ProductionContextService Context = new();
        public readonly ProductionContext Original = new()
        {
            WorkOrderId = "OLD-ID", WorkOrderNo = "OLD-WO", OperationName = "酸洗", ExecutionId = "OLD-EXEC",
            MachineCode = "A", IsPicklingMachineConfirmed = true, Status = "Running", StartedAt = DateTime.UtcNow.AddDays(-1)
        };
        public readonly FakeApi Api = new();
        public readonly FakeScanner Scanner = new();
        public readonly WorkExecutionPage OldExecution = new();
        public readonly Shell Shell;
        public Fixture()
        {
            Context.Set(Original);
            Shell = MachineSelectionTests.Shell.Current = new Shell
            {
                CreateScanner = () => new WorkStartScanPage(Api, Scanner, Context),
                CreateOrders = () => new WorkStartOrdersPage(Api, Context)
            };
            Shell.Stack.Add(OldExecution);
        }
        public void Switch() => OldExecution.ClickSwitch();
    }
    private static WorkOrderTaskDto Order(string machine, string number = "SHARED") =>
        new() { id = machine + number, workOrderNo = number, deviceCode = machine, operationName = "酸洗" };
    public static void Run()
    {
        var f = new Fixture(); var a = Order("A"); var b = Order("B");
        f.Api.Rows.AddRange(new[] { a, b, Order("B"), Order("") });
        f.Switch(); f.OldExecution.ClickSwitch();
        Check(f.Shell.CurrentPage is WorkStartScanPage && f.Shell.Stack.Count == 2, "Click opens exactly one scanner");
        Check(((WorkStartScanPage)f.Shell.CurrentPage).ScanHintLabel.Text!.Contains("选择工单"), "Scanner describes the new destination");
        f.Scanner.Scan(" B ").GetAwaiter().GetResult();
        var orders = (WorkStartOrdersPage)f.Shell.CurrentPage;
        Check(!f.Shell.Stack.Contains(f.OldExecution), "Old execution page is removed from navigation history");
        Check(f.Context.Current is null, "Successful binding clears old work order and acid-machine confirmation");
        Check(orders.Rows.Count == 1 && ReferenceEquals(orders.Rows[0], b), "Mixed-machine duplicate results retain the newly bound machine");
        Check(orders.HeaderLabel.Text!.Contains("机台 B"), "Header identifies the newly bound machine");
        Check(f.Api.StartCalls == 0, "Binding does not start a work order automatically");
        orders.Start(a); Check(f.Api.StartCalls == 0, "Old-machine row cannot start work");
        f.Api.StartResult = false; orders.Start(b);
        Check(f.Context.Current is null && ReferenceEquals(f.Shell.CurrentPage, orders), "Failed start stays on selection with no old session");
        f.Api.StartResult = true; orders.Start(b);
        Check(f.Shell.CurrentPage is WorkExecutionPage && !ReferenceEquals(f.Shell.CurrentPage, f.OldExecution), "Starting creates a new execution page");
        var current = f.Context.Current!;
        Check(current.MachineCode == "B" && current.WorkOrderNo == b.workOrderNo, "New session uses the selected order and bound machine");
        Check(current.SessionId != f.Original.SessionId && current.ExecutionId is null && !current.IsPicklingMachineConfirmed,
            "Old execution, session and acid confirmation are never reused");

        var g = new Fixture(); g.Switch(); var scan = g.Shell.CurrentPage; g.Api.BindResult = false;
        g.Scanner.Scan("C").GetAwaiter().GetResult();
        Check(ReferenceEquals(g.Context.Current, g.Original) && ReferenceEquals(g.Shell.CurrentPage, scan), "Failed binding preserves original session and scanner");
        g.Api.BindResult = true; g.Api.Rows.Add(Order("C")); g.Scanner.Scan("C").GetAwaiter().GetResult();
        var cOrders = (WorkStartOrdersPage)g.Shell.CurrentPage;
        Check(g.Context.Current is null && cOrders.Rows.Single().deviceCode == "C", "Binding failure can be retried to correct-machine selection");
        var stale = cOrders.Rows[0]; g.Api.Rows.Clear(); g.Api.Rows.Add(Order("A")); cOrders.Refresh();
        Check(cOrders.Rows.Count == 0, "Old-only backend results do not fall back to another machine");
        var calls = g.Api.StartCalls; cOrders.Start(stale);
        Check(g.Api.StartCalls == calls, "A row from before refresh cannot start work");
        g.Api.ThrowLoad = true; cOrders.Refresh(); Check(cOrders.Rows.Count == 0, "Failed loading clears stale clickable rows");
        g.Api.ThrowLoad = false; g.Api.Rows.Clear(); g.Api.Rows.Add(Order("C")); cOrders.Refresh();
        Check(cOrders.Rows.Count == 1, "Load can recover after failure");
        g.Api.PendingStart = new(); cOrders.Start(cOrders.Rows[0]); cOrders.Start(cOrders.Rows[0]);
        Check(g.Api.StartCalls == calls + 1, "Repeated start clicks send one request");
        g.Shell.GoToAsync("..").GetAwaiter().GetResult(); Wait(() => !cOrders.Starting);
        Check(g.Context.Current is null && g.Shell.CurrentPage is not WorkExecutionPage, "Late canceled start cannot reopen execution");

        var h = new Fixture(); h.Switch(); h.Api.PendingBind = new(); var pending = h.Scanner.Scan("LATE");
        h.Shell.GoToAsync("..").GetAwaiter().GetResult(); pending.GetAwaiter().GetResult();
        Check(ReferenceEquals(h.Context.Current, h.Original) && h.Api.LastBindToken.IsCancellationRequested, "Cancel before successful binding retains original session");

        var n = new Fixture(); n.Switch(); n.Shell.FailNextSelection = true; n.Scanner.Scan("B").GetAwaiter().GetResult();
        var retry = (WorkStartScanPage)n.Shell.CurrentPage;
        Check(n.Context.Current is null && retry.ScanHintLabel.Text!.Contains("重试"), "Navigation failure cannot restore old local session");
        retry.TapRetry();
        Check(n.Api.BindCalls == 1 && n.Shell.CurrentPage is WorkStartOrdersPage && !n.Shell.Stack.Contains(n.OldExecution), "Navigation retry reuses binding and removes old execution");

        var normal = new Fixture(); normal.Shell.GoToAsync(AppShell.RouteWorkStartScan).GetAwaiter().GetResult();
        normal.Api.Rows.Add(Order("B")); normal.Scanner.Scan("B").GetAwaiter().GetResult();
        Check(normal.Shell.CurrentPage is WorkStartOrdersPage && ((WorkStartOrdersPage)normal.Shell.CurrentPage).Rows.Single().deviceCode == "B", "Normal scan-to-start uses bound-machine selection");

        var r = new Fixture(); r.Switch(); r.Api.Rows.Add(Order("B")); r.Scanner.Scan("B").GetAwaiter().GetResult();
        ((WorkStartOrdersPage)r.Shell.CurrentPage).Rescan();
        Check(r.Shell.CurrentPage is WorkStartScanPage, "Rescan opens scanner rather than old execution");
        r.Api.Rows.Clear(); r.Api.Rows.Add(Order("D")); r.Scanner.Scan("D").GetAwaiter().GetResult();
        Check(((WorkStartOrdersPage)r.Shell.CurrentPage).Rows.Single().deviceCode == "D", "Rescan rebuilds selection for another machine");

        var v = new Fixture(); v.Switch(); var oldLoad = v.Api.PendingLoad = new();
        v.Scanner.Scan("B").GetAwaiter().GetResult(); var versioned = (WorkStartOrdersPage)v.Shell.CurrentPage;
        v.Api.Rows.Add(Order("C"));
        versioned.ApplyQueryAttributes(new Dictionary<string, object> { ["boundMachineCode"] = "C" });
        Check(versioned.Rows.Count == 1 && versioned.Rows[0].deviceCode == "C", "New binding refreshes without waiting for an older request that ignores cancellation");
        oldLoad.SetResult(new List<WorkOrderTaskDto> { Order("B") });
        Wait(() => versioned.Rows.Count == 1);
        Check(versioned.Rows[0].deviceCode == "C", "Late old-machine loading cannot overwrite a newer binding");

        var lifecycle = new Fixture();
        var page = new WorkStartOrdersPage(lifecycle.Api, lifecycle.Context);
        lifecycle.Shell.Stack.Add(page); lifecycle.Api.Rows.Add(Order("B"));
        page.ApplyQueryAttributes(new Dictionary<string, object> { ["boundMachineCode"] = "B" });
        page.AppearOnly();
        Check(lifecycle.Api.LoadCalls == 0, "Appearing before NavigatedTo keeps refresh pending until the navigation token is ready");
        page.NavigateToOnly();
        Check(lifecycle.Api.LoadCalls == 1 && page.Rows.Count == 1, "NavigatedTo drains the pending automatic refresh");
        page.Leave(); lifecycle.Api.Rows.Clear(); lifecycle.Api.Rows.Add(Order("B", "NEW-ON-RETURN"));
        page.AppearOnly();
        Check(lifecycle.Api.LoadCalls == 1, "Return before NavigatedTo must not consume refresh with a canceled token");
        page.NavigateToOnly();
        Check(lifecycle.Api.LoadCalls == 2 && page.Rows[0].workOrderNo == "NEW-ON-RETURN", "Return renews cancellation and automatically queries fresh data");
        lifecycle.Api.Rows.Clear(); lifecycle.Api.Rows.Add(Order("C", "PARAMETERS-LATE"));
        page.ApplyQueryAttributes(new Dictionary<string, object> { ["boundMachineCode"] = "C" });
        Check(lifecycle.Api.LoadCalls == 3 && page.Rows[0].workOrderNo == "PARAMETERS-LATE", "Parameters arriving after Appearing immediately trigger automatic refresh");
        lifecycle.Api.Rows.Clear(); lifecycle.Api.Rows.Add(Order("C", "REBIND-SAME"));
        var sameBinding = new Dictionary<string, object> { ["boundMachineCode"] = "C", ["refreshToken"] = "new-binding" };
        page.ApplyQueryAttributes(sameBinding);
        Check(lifecycle.Api.LoadCalls == 4 && page.Rows[0].workOrderNo == "REBIND-SAME", "Binding the same machine again has an explicit fresh-load token");
        page.ApplyQueryAttributes(sameBinding);
        Check(lifecycle.Api.LoadCalls == 4, "Redelivering the same binding parameters does not duplicate refresh");

        var firstRefresh = lifecycle.Api.PendingLoad = new(); page.Refresh();
        var secondRefresh = lifecycle.Api.PendingLoad = new(); page.Refresh();
        var errorsBefore = ErrorDialogService.Errors;
        firstRefresh.SetException(new Exception("late obsolete failure"));
        Check(page.RefreshContainer.IsRefreshing && !page.OrdersList.IsEnabled && ErrorDialogService.Errors == errorsBefore,
            "An obsolete request cannot hide the new refresh indicator or show a stale error");
        secondRefresh.SetResult(new List<WorkOrderTaskDto> { Order("C", "LATEST") });
        Wait(() => page.Rows.Count == 1 && page.Rows[0].workOrderNo == "LATEST");
        Check(!page.RefreshContainer.IsRefreshing && page.OrdersList.IsEnabled, "The current request alone completes the refresh UI");
        lifecycle.Api.ThrowLoad = true; var beforeError = lifecycle.Api.LoadCalls; page.Refresh();
        Check(lifecycle.Api.LoadCalls == beforeError + 1, "Closing an error dialog does not automatically retry in a loop");
        lifecycle.Api.ThrowLoad = false; page.Refresh();
        Check(page.Rows.Count == 1, "Manual refresh remains available after a failed automatic refresh");
        Check(AppShell.ProductionOrderSelectionRoute("C") != AppShell.ProductionOrderSelectionRoute("C"), "Each successful binding navigation carries a distinct refresh token");
        Check(AppShell.ProductionOrderSelectionRoute("B /?").Contains("B%20%2F%3F"), "Machine code is URL-encoded");
        Console.WriteLine("PASS: " + _checks + " machine-selection flow assertions");
    }
}
'@
$nl = [Environment]::NewLine
$appShell = 'public static class AppShell {' + $nl +
    'public const string RouteHome = "//Home", RouteRoleModule = "RoleModule", RouteWorkStartScan = "WorkStartScan", RouteWorkStartOrders = "WorkStartOrders", RouteWorkExecution = "WorkExecution", RouteWorkOrderInstruction = "WorkOrderInstruction";' + $routeMethod + '}'
$source = '#nullable enable' + $nl + 'using System; using System.Collections.Generic; using System.Linq; using System.Threading; using System.Threading.Tasks;' + $nl +
    'namespace MachineSelectionTests {' + $scanSource + $ordersSource + $contextSource + $dtoSource +
    'public partial class WorkExecutionPage {' + $switchMethod + '}' + $appShell + 'public static class ApiTools {' + $dedupMethod + '}' + $doubles + '}'
Add-Type -TypeDefinition $source
[MachineSelectionTests.Regression]::Run()
