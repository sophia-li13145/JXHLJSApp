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
internal sealed class HardwareScanInput : IDisposable
{
    private readonly Activity _activity;
    private readonly HardwareScanService _scanner;
    private readonly EditText _input;
    private readonly HardwareScanTextBuffer _buffer = new();
    private CancellationTokenSource? _pending;
    private bool _clearing;
    private bool _disposed;

    public HardwareScanInput(Activity activity, HardwareScanService scanner)
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

    private void OnStateChanged()
    {
        Clear();
        // Shell and modal navigation update CurrentPage after the Appearing callback.
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RefreshFocus();
            if (!_disposed) _input.PostDelayed(RefreshFocus, 100);
        });
    }

    private void RefreshFocus()
    {
        if (_disposed) return;
        if (!_scanner.CanReceiveKeyboardInput)
        {
            // Disabling focus first prevents Android's focus search from selecting this
            // invisible input again while an alert or a business operation is active.
            _input.FocusableInTouchMode = false;
            _input.Focusable = false;
            if (_input.HasFocus) _input.ClearFocus();
            return;
        }
        if (_input.HasFocus) return;
        if (_activity.CurrentFocus is EditText editor && !ReferenceEquals(editor, _input) && editor.IsShown && editor.Enabled)
            return;
        _input.Focusable = true;
        _input.FocusableInTouchMode = true;
        if (_input.RequestFocus()) Log.Information("Hardware keyboard input ready. Revision=newland-scan-20261102");
    }

    private void OnFocusChanged(object? sender, global::Android.Views.View.FocusChangeEventArgs e)
    {
        if (!e.HasFocus)
        {
            Clear();
            MainThread.BeginInvokeOnMainThread(RefreshFocus);
        }
    }

    private async void OnTextChanged(object? sender, global::Android.Text.TextChangedEventArgs e)
    {
        if (_clearing || _disposed) return;
        if (!_input.HasFocus || !_scanner.CanReceiveKeyboardInput)
        {
            Clear();
            return;
        }
        _pending?.Cancel();
        _pending?.Dispose();
        using var pending = new CancellationTokenSource();
        _pending = pending;
        _buffer.Update(_input.Text, _scanner.Generation, Environment.TickCount64);
        try
        {
            await Task.Delay(HardwareScanTextBuffer.SettleMilliseconds, pending.Token);
            if (!pending.IsCancellationRequested) Flush(force: true);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log.Warning(ex, "Hardware keyboard scan failed"); }
        finally
        {
            if (ReferenceEquals(_pending, pending)) _pending = null;
        }
    }

    private void OnEditorAction(object? sender, TextView.EditorActionEventArgs e)
    {
        if (_disposed || !_input.HasFocus) return;
        e.Handled = true;
        Flush(force: true);
    }

    private void Flush(bool force = false)
    {
        if (_disposed || !_scanner.CanReceiveKeyboardInput) { Clear(); return; }
        if (!_buffer.TryTake(_scanner.Generation, force ? long.MaxValue : Environment.TickCount64, out var text)) return;
        Clear();
        Log.Information("Hardware keyboard scan. Length={Length}", text?.Length ?? 0);
        _scanner.Publish(text);
    }

    private void Clear()
    {
        _pending?.Cancel();
        _buffer.Reset();
        _clearing = true;
        try { _input.Text = string.Empty; }
        finally { _clearing = false; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _scanner.StateChanged -= OnStateChanged;
        Clear();
        _input.TextChanged -= OnTextChanged;
        _input.EditorAction -= OnEditorAction;
        _input.FocusChange -= OnFocusChanged;
        (_input.Parent as ViewGroup)?.RemoveView(_input);
        _input.Dispose();
    }
}
