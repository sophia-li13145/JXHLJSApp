$ErrorActionPreference = 'Stop'
# Compile the actual Android input bridge against small native event/focus doubles.
# This exercises focus acquisition and text delivery, not only the decoded-result router.
$nativeSource = (Get-Content (Join-Path (Split-Path $PSScriptRoot -Parent) 'Platforms/Android/RightKeyScanInput.cs') -Raw).
    Replace('namespace JXHLJSApp.Platforms.Android;', '').
    Replace('using JXHLJSApp.Services;', '').Replace('using JXHLJSApp.Tools;', '')
$nativeUsings = "using System.Threading;`nusing Android.App;`nusing Android.Views;`nusing Android.Widget;`n"
$nativeSource = $nativeSource.Replace('using Android.App;', '').Replace('using Android.Views;', '').Replace('using Android.Widget;', '').Replace('using Serilog;', '')
$nativeStubs = @'
namespace Android.Graphics { public static class Color { public static object Transparent => new(); } }
namespace Android.Text { public class TextChangedEventArgs : EventArgs { } }
namespace Android.Views
{
    public enum ImportantForAccessibility { NoHideDescendants }
    public enum MotionEventActions { Down }
    public class MotionEvent { public MotionEventActions Action { get; set; } }
    public class View
    {
        public class FocusChangeEventArgs : EventArgs { public bool HasFocus { get; set; } }
        public class TouchEventArgs : EventArgs { public MotionEvent? Event { get; set; } public bool Handled { get; set; } }
    }
    public class ViewGroup
    {
        public class LayoutParams { public LayoutParams(int w, int h) { } }
        public void RemoveView(Android.Widget.EditText input) { }
    }
}
namespace Android.App
{
    public class Activity
    {
        public object? CurrentFocus { get; set; }
        public Android.Widget.EditText? Input { get; set; }
        public void AddContentView(Android.Widget.EditText input, Android.Views.ViewGroup.LayoutParams layout) => Input = input;
    }
}
namespace Android.Widget
{
    public class TextView
    {
        public class EditorActionEventArgs : EventArgs { public bool Handled { get; set; } }
    }
    public class EditText : IDisposable
    {
        private readonly Android.App.Activity _activity;
        private string? _text;
        private bool _selected;
        public EditText(Android.App.Activity activity) => _activity = activity;
        public float Alpha { get; set; }
        public bool Focusable { get; set; } = true;
        public bool FocusableInTouchMode { get; set; } = true;
        public bool ShowSoftInputOnFocus { get; set; } = true;
        public Android.Views.ImportantForAccessibility ImportantForAccessibility { get; set; }
        public bool HasFocus { get; private set; }
        public bool IsShown { get; set; } = true;
        public bool Enabled { get; set; } = true;
        public bool Disposed { get; private set; }
        public object Parent { get; } = new Android.Views.ViewGroup();
        public event EventHandler<Android.Text.TextChangedEventArgs>? TextChanged;
        public event EventHandler<TextView.EditorActionEventArgs>? EditorAction;
        public event EventHandler<Android.Views.View.FocusChangeEventArgs>? FocusChange;
        public event EventHandler<Android.Views.View.TouchEventArgs>? Touch;
        public string? Text
        {
            get => _text;
            set { _text = value; _selected = false; TextChanged?.Invoke(this, new()); }
        }
        public void Inject(string value) => Text = _selected ? value : (Text ?? "") + value;
        public void SelectAll() => _selected = true;
        public void SetBackgroundColor(object color) { }
        public void PostDelayed(Action action, int milliseconds) { }
        public bool RequestFocus()
        {
            if (!Focusable || Disposed) return false;
            if (HasFocus) return true;
            if (_activity.CurrentFocus is EditText old) old.ClearFocus();
            HasFocus = true; _activity.CurrentFocus = this;
            FocusChange?.Invoke(this, new() { HasFocus = true });
            return true;
        }
        public void ClearFocus()
        {
            if (!HasFocus) return;
            HasFocus = false;
            if (ReferenceEquals(_activity.CurrentFocus, this)) _activity.CurrentFocus = null;
            FocusChange?.Invoke(this, new() { HasFocus = false });
        }
        public bool Enter()
        {
            var args = new TextView.EditorActionEventArgs();
            EditorAction?.Invoke(this, args); return args.Handled;
        }
        public void Tap() => Touch?.Invoke(this, new() { Event = new() { Action = Android.Views.MotionEventActions.Down } });
        public void Dispose() => Disposed = true;
    }
}
'@
$nativeMethods = @'
    private sealed class PumpContext : System.Threading.SynchronizationContext
    {
        public override void Post(System.Threading.SendOrPostCallback callback, object? state) =>
            MainThread.BeginInvokeOnMainThread(() => callback(state));
    }
    private static void Pump(int milliseconds)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < milliseconds)
        { MainThread.Drain(); System.Threading.Thread.Sleep(5); }
        MainThread.Drain();
    }
    private static void RunNative()
    {
        var previousContext = System.Threading.SynchronizationContext.Current;
        System.Threading.SynchronizationContext.SetSynchronizationContext(new PumpContext());
        var scanner = new RightKeyScanService();
        var activity = new Android.App.Activity();
        RightKeyScanInput? bridge = null;
        try
        {
            var page = new Page();
            var ready = true;
            var codes = new List<string>();
            scanner.Register(page, code => { codes.Add(code); return Task.CompletedTask; }, () => ready);
            Application.Current = new Application { MainPage = page };
            page.Appear(); scanner.SetActive(true); scanner.SetWindowFocused(true);
            bridge = new RightKeyScanInput(activity, scanner);
            MainThread.Drain();
            var input = activity.Input!;
            Check(input.HasFocus && !input.ShowSoftInputOnFocus, "Quality scan page without Entry prepares a native target without a soft keyboard");
            input.Inject("Q"); Pump(250);
            Check(codes.Count == 0, "Partial device text must not trigger a business query");
            input.Inject("UALITY-001"); Pump(850);
            Check(codes.SequenceEqual(new[] { "QUALITY-001" }), "No-Enter device text reaches the actual quality page handler");
            Check(input.Text == "", "Consumed native scanner target is cleared");
            input.Inject("QUALITY-002");
            Check(input.Enter(), "Hardware Enter is consumed by the native adapter");
            Pump(850);
            Check(codes.Count == 2 && codes[1] == "QUALITY-002", "Enter and idle timer together submit exactly once");
            ready = false; Pump(300);
            Check(!input.Focusable, "In-page overlay or form loading disables native focus without navigation");
            ready = true; Pump(300);
            Check(input.HasFocus, "Completing asynchronous page setup automatically restores native scanning");
            input.Inject("STALE"); scanner.SetWindowFocused(false); Pump(850);
            Check(codes.Count == 2, "Native pending text is discarded on window focus loss");
            scanner.SetWindowFocused(true); MainThread.Drain();
            input.Inject("RESTORED"); input.Enter(); MainThread.Drain();
            Check(codes.Count == 3 && codes[2] == "RESTORED", "Returning from a system dialog restores native delivery");

            var home = new Page();
            var homeNative = new Android.Widget.EditText(activity);
            var entry = new Entry { Handler = new TestHandler { PlatformView = homeNative } };
            var homeCodes = new List<string>();
            scanner.Register(home, code => { homeCodes.Add(code); return Task.CompletedTask; }, textEntry: () => entry);
            page.Disappear(); Application.Current.MainPage = home; home.Appear(); MainThread.Drain();
            Check(homeNative.HasFocus && !homeNative.ShowSoftInputOnFocus, "Home uses its visible machine Entry as the scanner target");
            homeNative.Inject("P"); Pump(300); homeNative.Inject("CAP_A"); Pump(850);
            Check(homeCodes.SequenceEqual(new[] { "PCAP_A" }), "Readiness polling does not select and overwrite partially received machine text");
            homeNative.Inject("PCAP_B"); homeNative.Enter(); MainThread.Drain();
            Check(homeCodes.Count == 2 && homeCodes[1] == "PCAP_B", "A second machine scan replaces the preceding machine number");
            homeNative.Tap(); homeNative.Text = "MANUAL"; Pump(850);
            Check(homeCodes.Count == 2 && !bridge.IsAutomaticEntry(entry), "Touching the machine field enters manual editing and suppresses auto submission");
            Check(!homeNative.Enter(), "Manual Enter remains available to the existing confirmation handler");
            var otherEditor = new Android.Widget.EditText(activity);
            otherEditor.RequestFocus(); Pump(300);
            Check(otherEditor.HasFocus, "Scanner readiness polling does not steal another form editor");

            home.Disappear(); otherEditor.ClearFocus(); Application.Current.MainPage = page; page.Appear(); MainThread.Drain();
            input.Inject("NAVIGATION-STALE"); page.Disappear(); Application.Current.MainPage = new Page(); Pump(850);
            Check(codes.Count == 3, "Device text cannot reach a departed page or an unregistered page");
            scanner.SetActive(false); bridge.Dispose(); Pump(50);
            Check(input.Disposed && homeNative.ShowSoftInputOnFocus, "Disposal releases native target and restores page keyboard behavior");
        }
        finally
        {
            scanner.SetActive(false); bridge?.Dispose(); MainThread.Drain();
            System.Threading.SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

'@
$harness = Get-Content (Join-Path $PSScriptRoot 'RightKeyScan.Tests.ps1') -Raw
$harness = $harness.Replace('    private static void Send(', $nativeMethods + '    private static void Send(')
$harness = $harness.Replace('        Console.WriteLine(', "        RunNative();`n        Console.WriteLine(")
$harness = $harness.Replace('private static readonly Queue<Action> Pending = new();', 'private static readonly System.Collections.Concurrent.ConcurrentQueue<Action> Pending = new();')
$harness = $harness.Replace('while (Pending.Count > 0) Pending.Dequeue()();', 'while (Pending.TryDequeue(out var next)) next();')
$harness = $harness.Replace('public static void Error(Exception error,', 'public static void Warning(Exception error, string message, params object?[] args) { }' + "`n        public static void Error(Exception error,")
$harness = $harness.Replace('$productionSource.Replace(', '$nativeUsings + $productionSource.Replace(')
$harness = $harness.Replace('+ $protocolSource + $textSource + $testCases', '+ $protocolSource + $textSource + $nativeSource + $testCases')
$harness = $harness.Replace('+ $logStub', '+ $logStub + $nativeStubs')
$harness = $harness.Replace('$PSScriptRoot', "'" + $PSScriptRoot.Replace("'", "''") + "'")
$harness = $harness.Replace('right-key routing and protocol assertions', 'right-key routing, protocol and native-input assertions')
Invoke-Expression $harness
