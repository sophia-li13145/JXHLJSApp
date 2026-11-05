using Android.App;
using Android.Views;
using Android.Widget;
using JXHLJSApp.Services;
using JXHLJSApp.Tools;
using Serilog;

namespace JXHLJSApp.Platforms.Android;

/// <summary>
/// A native input target for devices that fill EditText instead of broadcasting.
/// It takes focus only on an eligible scan page and never steals an editor the user is using.
/// </summary>
internal sealed class RightKeyScanInput : IDisposable
{
    private readonly Activity _activity;
    private readonly RightKeyScanService _scanner;
    private readonly EditText _input;
    private readonly MachineScanTextBuffer _buffer = new();
    private EditText? _pageInput;
    private bool _pageInputShowsKeyboard;
    private bool _manualPageInput;
    private CancellationTokenSource? _pending;
    private bool _clearing;
    private bool _disposed;
    private Page? _textOwner;
    private CancellationTokenSource? _monitor;
    private bool _refreshSelection = true;

    public RightKeyScanInput(Activity activity, RightKeyScanService scanner)
    {
        _activity = activity;
        _scanner = scanner;
        _input = new EditText(activity)
        {
            Alpha = 0,
            Focusable = false,
            FocusableInTouchMode = false,
            ShowSoftInputOnFocus = false,
            ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants
        };
        _input.SetBackgroundColor(global::Android.Graphics.Color.Transparent);
        activity.AddContentView(_input, new ViewGroup.LayoutParams(1, 1));
        _input.TextChanged += OnTextChanged;
        _input.EditorAction += OnEditorAction;
        _input.FocusChange += OnFocusChanged;
        _scanner.StateChanged += OnStateChanged;
        OnStateChanged();
    }

    public bool IsAutomaticEntry(Entry entry) =>
        !_manualPageInput && ReferenceEquals(entry.Handler?.PlatformView, _pageInput);

    private void OnStateChanged()
    {
        Clear();
        _refreshSelection = true;
        if (!_scanner.IsForeground)
        {
            _monitor?.Cancel();
            _monitor = null;
        }
        else if (_monitor is null)
        {
            _monitor = new CancellationTokenSource();
            _ = MonitorReadinessAsync(_monitor);
        }
        // Shell and modal navigation update CurrentPage after the Appearing callback.
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RefreshFocus();
            if (!_disposed) _input.PostDelayed(RefreshFocus, 100);
        });
    }

    private async Task MonitorReadinessAsync(CancellationTokenSource monitor)
    {
        using (monitor)
        {
            try
            {
                // Initial form loads and in-page overlays can change eligibility without navigation.
                while (!monitor.IsCancellationRequested)
                {
                    await Task.Delay(250, monitor.Token);
                    RefreshFocus();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Log.Warning(ex, "Scanner input readiness check failed"); }
            finally
            {
                if (ReferenceEquals(_monitor, monitor)) _monitor = null;
            }
        }
    }

    private void RefreshFocus()
    {
        if (_disposed) return;
        SetPageInput(_scanner.TextEntry?.Handler?.PlatformView as EditText);
        if (!_scanner.CanReceiveText)
        {
            // Disabling focus first prevents Android's focus search from selecting this
            // invisible input again while an alert or a business operation is active.
            _input.FocusableInTouchMode = false;
            _input.Focusable = false;
            if (_input.HasFocus) _input.ClearFocus();
            return;
        }
        if (_pageInput is not null)
        {
            _input.FocusableInTouchMode = false;
            _input.Focusable = false;
            if (_manualPageInput) return;
            // A delayed layout/focus retry must not select a partially received barcode
            // and cause the remaining wedge characters to overwrite its prefix.
            if (_pageInput.HasFocus && _pending is not null) return;
            if (_pageInput.HasFocus && !_refreshSelection) return;
            if (_activity.CurrentFocus is EditText other && !ReferenceEquals(other, _pageInput) &&
                !ReferenceEquals(other, _input) && other.IsShown && other.Enabled) return;
            _pageInput.ShowSoftInputOnFocus = false;
            if (_pageInput.RequestFocus())
            {
                // Replacing selected text keeps successive scans from appending to the previous code.
                _pageInput.SelectAll();
                _refreshSelection = false;
                Log.Information("Scan text input ready. Target=page-entry, Revision=unified-scan-20261104");
            }
            return;
        }
        if (_input.HasFocus) return;
        if (_activity.CurrentFocus is EditText editor && !ReferenceEquals(editor, _input) && editor.IsShown && editor.Enabled)
            return;
        _input.Focusable = true;
        _input.FocusableInTouchMode = true;
        if (_input.RequestFocus()) Log.Information("Scan text input ready. Target=native, Revision=unified-scan-20261104");
    }

    private void SetPageInput(EditText? input)
    {
        if (ReferenceEquals(_pageInput, input)) return;
        Clear();
        if (_pageInput is not null)
        {
            _pageInput.TextChanged -= OnTextChanged;
            _pageInput.EditorAction -= OnEditorAction;
            _pageInput.FocusChange -= OnFocusChanged;
            _pageInput.Touch -= OnPageInputTouched;
            _pageInput.ShowSoftInputOnFocus = _pageInputShowsKeyboard;
        }
        _pageInput = input;
        _manualPageInput = false;
        if (input is null) return;
        _pageInputShowsKeyboard = input.ShowSoftInputOnFocus;
        input.TextChanged += OnTextChanged;
        input.EditorAction += OnEditorAction;
        input.FocusChange += OnFocusChanged;
        input.Touch += OnPageInputTouched;
    }

    private void OnPageInputTouched(object? sender, global::Android.Views.View.TouchEventArgs e)
    {
        if (e.Event?.Action == MotionEventActions.Down && _pageInput is not null)
        {
            // Only an actual touch enters manual editing; programmatic scan focus does not.
            _manualPageInput = true;
            _pageInput.ShowSoftInputOnFocus = _pageInputShowsKeyboard;
            Clear();
        }
        e.Handled = false;
    }

    private void OnFocusChanged(object? sender, global::Android.Views.View.FocusChangeEventArgs e)
    {
        if (!e.HasFocus)
        {
            if (ReferenceEquals(sender, _pageInput)) _manualPageInput = false;
            _refreshSelection = true;
            Clear();
            MainThread.BeginInvokeOnMainThread(RefreshFocus);
        }
    }

    private async void OnTextChanged(object? sender, global::Android.Text.TextChangedEventArgs e)
    {
        if (_clearing || _disposed) return;
        var source = sender as EditText;
        if (source is null || !source.HasFocus || !_scanner.CanReceiveText ||
            (ReferenceEquals(source, _pageInput) && _manualPageInput))
        {
            Clear();
            return;
        }
        _pending?.Cancel();

        using var pending = new CancellationTokenSource();
        _pending = pending;
        _textOwner = _scanner.TextOwner;
        _buffer.Update(source.Text, _scanner.InputRevision, Environment.TickCount64);
        try
        {
            // Start the idle window after Android has finished the current edit event.
            // A cold PDA may spend longer than the window inside native text/IME callbacks.
            await Task.Yield();
            await Task.Delay(MachineScanTextBuffer.SettleMilliseconds, pending.Token);
            if (ReferenceEquals(_pending, pending) && !pending.IsCancellationRequested) Flush(force: true);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log.Warning(ex, "Device text scan failed"); }
        finally
        {
            if (ReferenceEquals(_pending, pending)) _pending = null;
        }
    }

    private void OnEditorAction(object? sender, TextView.EditorActionEventArgs e)
    {
        if (_disposed || sender is not EditText source || !source.HasFocus ||
            (ReferenceEquals(source, _pageInput) && _manualPageInput)) return;
        e.Handled = true;
        Flush(force: true);
    }

    private void Flush(bool force = false)
    {
        if (_disposed || !_scanner.CanReceiveText) { Clear(); return; }
        if (!_buffer.TryTake(_scanner.InputRevision, force ? long.MaxValue : Environment.TickCount64, out var text)) return;
        var owner = _textOwner;
        var revision = _scanner.InputRevision;
        Clear();
        Log.Information("Device text scan. Page={Page}, Length={Length}", owner?.GetType().Name, text?.Length ?? 0);
        if (owner is not null && text is not null) _scanner.Submit(owner, revision, text);
    }

    private void Clear()
    {
        _pending?.Cancel();
        _buffer.Reset();
        _textOwner = null;
        _clearing = true;
        try { _input.Text = string.Empty; }
        finally { _clearing = false; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _monitor?.Cancel();
        _monitor = null;
        _scanner.StateChanged -= OnStateChanged;
        SetPageInput(null);
        Clear();
        _input.TextChanged -= OnTextChanged;
        _input.EditorAction -= OnEditorAction;
        _input.FocusChange -= OnFocusChanged;
        (_input.Parent as ViewGroup)?.RemoveView(_input);
        _input.Dispose();
    }
}
