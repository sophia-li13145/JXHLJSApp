$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path (Split-Path $PSScriptRoot -Parent) 'Services/RightKeyScanService.cs'
$productionSource = (Get-Content -LiteralPath $sourcePath -Raw).Replace(
    'namespace JXHLJSApp.Services;', 'namespace JXHLJSApp.RightKeyScanTests;')
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
public class Entry { public TestHandler? Handler { get; set; } }
public class TestHandler { public object? PlatformView { get; set; } }
public class Navigation { public List<Page> ModalStack { get; } = new(); }
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

public static class RightKeyScanRegression
{
    private static int _assertions;
    public static void Run()
    {
        var scanner = new RightKeyScanService();
        var shell = new Shell();
        var first = new Page();
        var second = new Page();
        Application.Current = new Application { MainPage = shell };
        shell.CurrentPage = first;
        var firstCalls = 0;
        var secondCalls = 0;
        string? received = null;
        var enabled = true;
        scanner.Register(first, code => { firstCalls++; received = code; return Task.CompletedTask; }, () => enabled);
        scanner.Register(second, code => { secondCalls++; return Task.CompletedTask; });
        first.Appear();

        Send(scanner, "background");
        Check(firstCalls == 0, "Background scans must be ignored");
        scanner.SetActive(true);
        scanner.SetWindowFocused(true);
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
        scanner.Register(modalContent, code => { modalCalls++; return Task.CompletedTask; });
        modalContent.Appear();
        Send(scanner, "modal");
        Check(modalCalls == 1 && firstCalls == 2, "Only the top modal content receives a result");
        modalContent.Disappear();
        shell.Navigation.ModalStack.Clear();
        first.Appear();
        first.Appear();
        Send(scanner, "returned");
        Check(firstCalls == 3, "Repeated Appearing must not duplicate registration");

        scanner.Receive("queued");
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
        scanner.Receive("queued-before-background");
        scanner.SetActive(false);
        scanner.SetActive(true);
        scanner.SetWindowFocused(true);
        MainThread.Drain();
        Check(secondCalls == 1, "Queued result must not survive a foreground transition");

        var processing = new Page();
        var pending = new TaskCompletionSource();
        var nextPending = new TaskCompletionSource();
        var processingCalls = 0;
        scanner.Register(processing, code => { processingCalls++; return processingCalls == 1 ? pending.Task : nextPending.Task; });
        shell.CurrentPage = processing;
        processing.Appear();
        Send(scanner, "one");
        Send(scanner, "two");
        Check(processingCalls == 1, "An in-flight operation must reject a second scan");
        processing.Disappear();
        shell.CurrentPage = first;
        first.Appear();
        var beforeReturningScan = firstCalls;
        Send(scanner, "AFTER-BACK");
        Check(firstCalls == beforeReturningScan + 1, "A pending operation on the departed page must not block the returned scan page");
        shell.CurrentPage = processing;
        processing.Appear();
        Send(scanner, "NEW-OPERATION");
        Check(processingCalls == 2, "Returning to the same page starts a new scan session despite the old pending operation");
        pending.SetResult();
        Send(scanner, "WHILE-NEW-OPERATION-PENDING");
        Check(processingCalls == 2, "Late completion of the old session must not unlock the new in-flight scan");
        nextPending.SetResult();

        var failing = new Page();
        var failures = 0;
        scanner.Register(failing, code => { failures++; throw new InvalidOperationException("expected"); });
        shell.CurrentPage = failing;
        failing.Appear();
        Send(scanner, "failure1");
        Send(scanner, "failure2");
        Check(failures == 2 && ErrorDialogService.Errors == 2, "Exceptions must release processing state");

        var tabContent = new Page();
        Application.Current.MainPage = new FlyoutPage { Detail = new TabbedPage { CurrentPage = new NavigationPage { CurrentPage = tabContent } } };
        var tabCalls = 0;
        scanner.Register(tabContent, code => { tabCalls++; return Task.CompletedTask; });
        tabContent.Appear();
        Send(scanner, "nested");
        Check(tabCalls == 1, "Nested navigation containers resolve to the visible content");

        // A burst queued before a step transition must execute only once.
        var burst = new Page();
        var burstCalls = 0;
        scanner.Register(burst, _ => { burstCalls++; return Task.CompletedTask; });
        Application.Current.MainPage = burst;
        burst.Appear();
        scanner.Receive("burst-1");
        scanner.Receive("burst-2");
        MainThread.Drain();
        Check(burstCalls == 1, "Queued scans must not spill into the next business step");

        // Eligibility is checked at arrival as well as dispatch.
        var gated = new Page();
        var ready = false;
        var gatedCalls = 0;
        scanner.Register(gated, _ => { gatedCalls++; return Task.CompletedTask; }, () => ready);
        Application.Current.MainPage = gated;
        gated.Appear();
        scanner.Receive("while-disabled");
        ready = true;
        MainThread.Drain();
        Check(gatedCalls == 0, "A scan received before the form is ready is discarded");
        Send(scanner, "after-enabled");
        Check(gatedCalls == 1, "The same visible page becomes ready without focus manipulation");

        var fields = new Dictionary<string, string?> { ["SCAN_STATE"] = "ok", ["SCAN_BARCODE1"] = "QR-1" };
        string? Read(string key) => fields.TryGetValue(key, out var value) ? value : null;
        Check(RightKeyScanProtocol.ReadResult(Read, "SCAN_BARCODE1") == "QR-1", "Standard result field");
        fields["SCAN_STATE"] = "fail";
        Check(RightKeyScanProtocol.ReadResult(Read, "SCAN_BARCODE1") == null, "Failed decode is ignored");
        fields.Remove("SCAN_STATE");
        fields["SCAN_BARCODE1"] = "";
        fields["SCAN_BARCODE2"] = "QR-2";
        Check(RightKeyScanProtocol.ReadResult(Read, "SCAN_BARCODE1") == "QR-2", "Second Newland result field");
        fields["custom"] = "QR-custom";
        Check(RightKeyScanProtocol.ReadResult(Read, "custom") == "QR-custom", "Explicit configured field");
        Check(RightKeyScanProtocol.ReadResult(Read, "missing") == "QR-2", "Configured field may fall back to a standard device field");
        fields.Clear();
        fields["data"] = "LEGACY-MACHINE";
        Check(RightKeyScanProtocol.ReadResult(Read, "SCAN_BARCODE1") == "LEGACY-MACHINE", "Existing lc/data devices work without reconfiguration");
        Check(RightKeyScanProtocol.GetActions(null).Contains("lc") &&
              RightKeyScanProtocol.GetActions(null).Contains(RightKeyScanProtocol.DefaultAction),
            "Both existing and Newland broadcast modes are accepted");
        Check(RightKeyScanProtocol.GetActions("custom", "legacy-custom").Distinct().Count() == 4,
            "Persisted custom broadcast action is preserved");
        fields["SCAN_STATE"] = "fail";
        Check(RightKeyScanProtocol.ReadResult(Read, "data") == null, "Failed decode cannot fall back to stale legacy data");
        var multiStep = new Page();
        var step = 0;
        var steps = new List<string>();
        scanner.Register(multiStep, code =>
        {
            steps.Add((step == 0 ? "machine:" : "material:") + code);
            step++;
            return Task.CompletedTask;
        }, () => step < 2);
        Application.Current.MainPage = multiStep;
        multiStep.Appear();
        Send(scanner, "MACHINE");
        Send(scanner, "MATERIAL");
        Send(scanner, "AFTER-CONFIRM");
        Check(steps.SequenceEqual(new[] { "machine:MACHINE", "material:MATERIAL" }),
            "Machine-to-material workflow advances without a camera session and stops at confirmation");

        var childModal = new NavigationPage();
        var childContent = new Page();
        childModal.CurrentPage = childContent;
        // MAUI exposes the global modal stack through children as well.
        childContent.Navigation.ModalStack.Add(childModal);
        var modalShell = new Shell { CurrentPage = multiStep };
        modalShell.Navigation.ModalStack.Add(childModal);
        Application.Current.MainPage = modalShell;
        var childCalls = 0;
        scanner.Register(childContent, _ => { childCalls++; return Task.CompletedTask; });
        childContent.Appear();
        Send(scanner, "SHARED-MODAL-STACK");
        Check(childCalls == 1, "Shared modal navigation proxy must not loop or dispatch to the parent");

        var home = new Page();
        var homeCalls = 0;
        string? machineCode = null;
        var homeReady = true;
        scanner.Register(home, code => { homeCalls++; machineCode = code; return Task.CompletedTask; }, () => homeReady);
        Application.Current.MainPage = home;
        home.Appear();
        Check(scanner.IsReady(home), "Home machine input is eligible after appearing");
        var buffer = new MachineScanTextBuffer();
        var homeRevision = scanner.InputRevision;
        buffer.Update("P", homeRevision, 0);
        Check(!buffer.TryTake(homeRevision, 200, out _), "A cold PDA first character must not bind a partial machine code");
        buffer.Update("PCAP_A", homeRevision, 300);
        Check(!buffer.TryTake(homeRevision, 1049, out _), "Wait until the complete machine code settles");
        Check(buffer.TryTake(homeRevision, 1050, out var fullCode) && fullCode == "PCAP_A",
            "PDA direct text without Enter yields the complete machine code");
        scanner.Submit(home, homeRevision, fullCode!);
        MainThread.Drain();
        Check(homeCalls == 1 && machineCode == "PCAP_A", "Home text output reaches the same machine binding handler as broadcasts");
        Check(!buffer.TryTake(homeRevision, 2000, out _), "A text result is submitted exactly once");
        scanner.Receive("PCAP_A");
        MainThread.Drain();
        Check(homeCalls == 1, "A duplicate broadcast after text output must not bind twice");
        homeReady = false;
        scanner.Submit(home, scanner.InputRevision, "BUSY");
        MainThread.Drain();
        Check(homeCalls == 1, "Manual binding or navigation blocks text submissions");
        homeReady = true;
        var beforeFocusChange = scanner.InputRevision;
        buffer.Update("STALE", beforeFocusChange, 3000);
        scanner.SetWindowFocused(false);
        scanner.SetWindowFocused(true);
        Check(!buffer.TryTake(scanner.InputRevision, 4000, out _), "Focus transition clears partial scanner text");
        scanner.Submit(home, beforeFocusChange, "LATE");
        MainThread.Drain();
        Check(homeCalls == 1, "Delayed text from before a dialog cannot bind on return");
        var beforeLeave = scanner.InputRevision;
        home.Disappear();
        Application.Current.MainPage = first;
        first.Appear();
        scanner.Submit(home, beforeLeave, "WRONG-PAGE");
        MainThread.Drain();
        Check(homeCalls == 1 && !scanner.IsReady(home), "Home text cannot be delivered after navigation");
        home.Appear();
        Application.Current.MainPage = home;
        buffer.Update("SECOND-MACHINE", scanner.InputRevision, 5000);
        Check(buffer.TryTake(scanner.InputRevision, 5750, out var nextCode), "Returning home accepts a fresh complete scan");
        scanner.Submit(home, scanner.InputRevision, nextCode!);
        MainThread.Drain();
        Check(homeCalls == 2 && machineCode == "SECOND-MACHINE", "Home recovers after navigating back");
        Console.WriteLine("PASS: " + _assertions + " right-key routing and protocol assertions");
    }

    private static void Send(RightKeyScanService scanner, string code)
    { scanner.Receive(code); MainThread.Drain(); }
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
$protocolSource = (Get-Content -LiteralPath (Join-Path (Split-Path $PSScriptRoot -Parent) 'Tools/RightKeyScanProtocol.cs') -Raw).Replace('namespace JXHLJSApp.Tools;', '')
$textSource = (Get-Content -LiteralPath (Join-Path (Split-Path $PSScriptRoot -Parent) 'Tools/MachineScanTextBuffer.cs') -Raw).Replace('namespace JXHLJSApp.Tools;', '')
$testSource = "#nullable enable`nusing System;`nusing System.Collections.Generic;`nusing System.Linq;`nusing System.Threading.Tasks;`n" +
    $productionSource.Replace('namespace JXHLJSApp.RightKeyScanTests;', "namespace JXHLJSApp.RightKeyScanTests`n{") + $protocolSource + $textSource + $testCases + "`n}`n" + $logStub
Add-Type -TypeDefinition $testSource
[JXHLJSApp.RightKeyScanTests.RightKeyScanRegression]::Run()
