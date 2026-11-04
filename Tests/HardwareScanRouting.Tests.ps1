$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path (Split-Path $PSScriptRoot -Parent) 'Services/HardwareScanService.cs'
$productionSource = (Get-Content -LiteralPath $sourcePath -Raw).Replace(
    'namespace JXHLJSApp.Services;', 'namespace JXHLJSApp.HardwareScanTests;')
# Exercise the production router with small platform doubles; no Android device is required.
$testCases = @'

public class Page
{
    public event Action<object?, EventArgs>? Appearing;
    public event Action<object?, EventArgs>? Disappearing;
    public Navigation Navigation { get; } = new();
    public void Appear() => Appearing?.Invoke(this, EventArgs.Empty);
    public void Disappear() => Disappearing?.Invoke(this, EventArgs.Empty);
}
public class Navigation { public List<Page> ModalStack { get; } = new(); }
public class Entry { }
public class Shell : Page { public Page? CurrentPage { get; set; } }
public class NavigationPage : Page { public Page? CurrentPage { get; set; } }
public class TabbedPage : Page { public Page? CurrentPage { get; set; } }
public class FlyoutPage : Page { public Page? Detail { get; set; } }
public class Application
{
    public static Application? Current { get; set; }
    public Page? MainPage { get; set; }
}
public static class MainThread
{
    private static readonly Queue<Action> Pending = new();
    public static void BeginInvokeOnMainThread(Action action) => Pending.Enqueue(action);
    public static void Drain() { while (Pending.Count > 0) Pending.Dequeue()(); }
}
public static class ErrorDialogService
{
    public static bool IsDialogVisible { get; set; }
    public static int Errors;
    public static Task ShowAsync(Page? owner, string title, string message)
    { Errors++; return Task.CompletedTask; }
}

public static class HardwareScanRegression
{
    private static int _assertions;
    public static void Run()
    {
        var scanner = new HardwareScanService();
        var shell = new Shell();
        var first = new Page();
        var second = new Page();
        Application.Current = new Application { MainPage = shell };
        shell.CurrentPage = first;
        var firstCalls = 0;
        var secondCalls = 0;
        string? received = null;
        var enabled = true;
        scanner.Attach(first, code => { firstCalls++; received = code; return Task.CompletedTask; }, () => enabled);
        scanner.Attach(second, code => { secondCalls++; return Task.CompletedTask; });
        first.Appear();

        Send(scanner, "background");
        Check(firstCalls == 0, "Background scans must be ignored");
        scanner.SetForeground(true);
        Send(scanner, "\uFEFF\u0002 JSON\r\ninside\u001Dvalue \u0003\r\n");
        Check(firstCalls == 1 && received == "JSON\r\ninside\u001Dvalue", "Normalize framing without corrupting payload");
        Send(scanner, received!);
        Check(firstCalls == 1, "Duplicate result must be ignored");
        Send(scanner, "other");
        Check(firstCalls == 2, "A different barcode must be accepted immediately");
        Send(scanner, "\r\n\u0000 ");
        Check(firstCalls == 2, "Empty result must be ignored");

        enabled = false;
        Send(scanner, "disabled");
        Check(firstCalls == 2, "Business state must gate hardware input");
        enabled = true;
        ErrorDialogService.IsDialogVisible = true;
        Send(scanner, "dialog");
        Check(firstCalls == 2, "Error dialog must block underlying page");
        ErrorDialogService.IsDialogVisible = false;

        scanner.SetWindowFocused(false);
        Send(scanner, "native-dialog");
        Check(firstCalls == 2, "A native alert/system window must suspend page input");
        scanner.SetWindowFocused(true);

        var modal = new NavigationPage();
        var modalContent = new Page();
        modal.CurrentPage = modalContent;
        shell.Navigation.ModalStack.Add(modal);
        Send(scanner, "unregistered-modal");
        Check(firstCalls == 2, "An unregistered modal must not fall back to its parent");
        var modalCalls = 0;
        scanner.Attach(modalContent, code => { modalCalls++; return Task.CompletedTask; });
        modalContent.Appear();
        Send(scanner, "modal");
        Check(modalCalls == 1 && firstCalls == 2, "Only the top modal content receives a result");
        modalContent.Disappear();
        shell.Navigation.ModalStack.Clear();
        first.Appear();
        first.Appear();
        Send(scanner, "returned");
        Check(firstCalls == 3, "Repeated Appearing must not duplicate registration");

        scanner.Publish("queued");
        first.Disappear();
        shell.CurrentPage = second;
        second.Appear();
        MainThread.Drain();
        Check(firstCalls == 3 && secondCalls == 0, "Queued result must not cross navigation");
        Send(scanner, "other");
        Check(secondCalls == 1, "Same code may be used on the next page/step");
        second.Disappear();
        Send(scanner, "departed");
        Check(secondCalls == 1, "Disappeared page must not receive input");
        second.Appear();
        scanner.Publish("queued-before-background");
        scanner.SetForeground(false);
        scanner.SetForeground(true);
        MainThread.Drain();
        Check(secondCalls == 1, "Queued result must not survive a foreground transition");

        var processing = new Page();
        var pending = new TaskCompletionSource();
        var processingCalls = 0;
        scanner.Attach(processing, code => { processingCalls++; return pending.Task; });
        shell.CurrentPage = processing;
        processing.Appear();
        Send(scanner, "one");
        Send(scanner, "two");
        Check(processingCalls == 1, "An in-flight operation must reject a second scan");
        pending.SetResult();

        var failing = new Page();
        var failures = 0;
        scanner.Attach(failing, code => { failures++; throw new InvalidOperationException("expected"); });
        shell.CurrentPage = failing;
        failing.Appear();
        Send(scanner, "failure1");
        Send(scanner, "failure2");
        Check(failures == 2 && ErrorDialogService.Errors == 2, "Exceptions must release processing state");

        var tabContent = new Page();
        Application.Current.MainPage = new FlyoutPage { Detail = new TabbedPage { CurrentPage = new NavigationPage { CurrentPage = tabContent } } };
        var tabCalls = 0;
        scanner.Attach(tabContent, code => { tabCalls++; return Task.CompletedTask; });
        tabContent.Appear();
        Send(scanner, "nested");
        Check(tabCalls == 1, "Nested navigation containers resolve to the visible content");

        var home = new Page();
        var homeReady = false;
        Entry? homeEntry = null;
        var homeCalls = 0;
        var refreshes = 0;
        scanner.StateChanged += () => refreshes++;
        scanner.Attach(home, code => { homeCalls++; return Task.CompletedTask; }, () => homeReady, () => homeEntry);
        Application.Current.MainPage = home;
        home.Appear();
        Check(!scanner.CanReceiveKeyboardInput, "Home profile loading initially prevents focus acquisition");
        homeEntry = new Entry();
        homeReady = true;
        var beforeRefresh = refreshes;
        scanner.Refresh(home);
        Check(refreshes == beforeRefresh + 1 && scanner.CanReceiveKeyboardInput,
            "Finishing asynchronous home initialization notifies native input to acquire focus");
        Check(ReferenceEquals(scanner.KeyboardEntry, homeEntry), "Home machine Entry is the preferred scanner input");
        Send(scanner, "MACHINE-001");
        Check(homeCalls == 1, "Home machine scans use the registered business callback");
        homeEntry = new Entry();
        scanner.Refresh(home);
        Check(ReferenceEquals(scanner.KeyboardEntry, homeEntry), "Recreated native handlers resolve the current machine Entry");
        beforeRefresh = refreshes;
        scanner.Refresh(first);
        Check(refreshes == beforeRefresh, "A late background page refresh must not disturb foreground input");
        scanner.Publish("queued-before-rebuild");
        scanner.Refresh(home);
        MainThread.Drain();
        Check(homeCalls == 1, "Text queued against a replaced home input is discarded");
        homeReady = false;
        scanner.Refresh(home);
        Check(!scanner.CanReceiveKeyboardInput, "Binding/navigation state suspends the home input");
        homeReady = true;
        scanner.Refresh(home);
        Check(scanner.CanReceiveKeyboardInput, "A failed bind can re-enable the same page without navigating away");
        home.Disappear();
        Check(scanner.KeyboardEntry == null, "A departed home page must release its preferred input");
        Console.WriteLine("PASS: " + _assertions + " hardware scan routing assertions");
    }

    private static void Send(HardwareScanService scanner, string code)
    { scanner.Publish(code); MainThread.Drain(); }
    private static void Check(bool condition, string message)
    { _assertions++; if (!condition) throw new Exception(message); }
}
'@
$logStub = @'
namespace Serilog
{
    public static class Log
    {
        public static void Information(string message, params object?[] args) { }
        public static void Error(Exception error, string message, params object?[] args) { }
    }
}
'@
$testSource = "#nullable enable`nusing System;`nusing System.Collections.Generic;`nusing System.Linq;`nusing System.Threading.Tasks;`n" +
    $productionSource.Replace('namespace JXHLJSApp.HardwareScanTests;', "namespace JXHLJSApp.HardwareScanTests`n{") + $testCases + "`n}`n" + $logStub
Add-Type -TypeDefinition $testSource
[JXHLJSApp.HardwareScanTests.HardwareScanRegression]::Run()
