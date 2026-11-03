using Serilog;

namespace JXHLJSApp.Services;

/// <summary>Routes decoded hardware barcodes exclusively to the foreground page.</summary>
public sealed class HardwareScanService
{
    private readonly List<Registration> _registrations = new();
    private bool _foreground;
    private bool _windowFocused = true;
    private bool _processing;
    private string? _lastCode;
    private long _lastAt;
    private Page? _lastOwner;
    private int _generation;

    public event Action? StateChanged;
    internal int Generation => _generation;
    internal bool CanReceiveKeyboardInput =>
        _foreground && _windowFocused && !_processing && !ErrorDialogService.IsDialogVisible &&
        _registrations.LastOrDefault(item => ReferenceEquals(item.Owner, GetForegroundPage()))?.CanScan() == true;

    public void SetForeground(bool foreground)
    {
        _foreground = foreground;
        _lastCode = null;
        _generation++;
        StateChanged?.Invoke();
    }

    public void SetWindowFocused(bool focused)
    {
        _windowFocused = focused;
        _generation++;
        StateChanged?.Invoke();
    }

    public void Attach(Page owner, Func<string, Task> handle, Func<bool>? canScan = null)
    {
        var registration = new Registration(owner, handle, canScan ?? (() => true));
        owner.Appearing += (_, _) =>
        {
            _registrations.Remove(registration);
            _registrations.Add(registration);
            _generation++;
            StateChanged?.Invoke();
        };
        owner.Disappearing += (_, _) =>
        {
            _registrations.Remove(registration);
            if (ReferenceEquals(_lastOwner, owner))
            {
                _lastOwner = null;
                _lastCode = null;
            }
            _generation++;
            StateChanged?.Invoke();
        };
    }

    public void Publish(string? rawCode)
    {
        // Never deliver an input queued before a navigation/background transition to a new page.
        var generation = _generation;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (generation == _generation) await DispatchAsync(rawCode);
        });
    }

    private async Task DispatchAsync(string? rawCode)
    {
        if (!_foreground || !_windowFocused || _processing || ErrorDialogService.IsDialogVisible)
        {
            Log.Information("Hardware scan ignored. Foreground={Foreground}, Focused={Focused}, Processing={Processing}, Dialog={Dialog}",
                _foreground, _windowFocused, _processing, ErrorDialogService.IsDialogVisible);
            return;
        }
        var owner = GetForegroundPage();
        var target = _registrations.LastOrDefault(item => ReferenceEquals(item.Owner, owner));
        var code = Normalize(rawCode);
        if (target is null || code is null)
        {
            Log.Information("Hardware scan ignored. Page={Page}, Registered={Registered}, Empty={Empty}",
                owner?.GetType().Name, target is not null, code is null);
            return;
        }

        try
        {
            if (!target.CanScan())
            {
                Log.Information("Hardware scan ignored. Page={Page}, Reason=business-state", owner?.GetType().Name);
                return;
            }
            var now = Environment.TickCount64;
            if (ReferenceEquals(_lastOwner, owner) && code == _lastCode && now - _lastAt < 500) return;
            _lastCode = code;
            _lastOwner = owner;
            _lastAt = now;
            _processing = true;
            StateChanged?.Invoke();
            Log.Information("Hardware scan dispatched. Page={Page}, Length={Length}", owner?.GetType().Name, code.Length);
            await target.Handle(code);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Hardware scan handling failed. Page={Page}", owner?.GetType().Name);
            await ErrorDialogService.ShowAsync(owner, "扫码失败", ex.Message);
        }
        finally
        {
            _processing = false;
            StateChanged?.Invoke();
        }
    }

    internal static string? Normalize(string? code)
    {
        // Strip framing characters only at the edges; preserve JSON and GS inside GS1 data.
        var value = code?.Trim(' ', '\t', '\r', '\n', '\uFEFF', '\u0000', '\u0002', '\u0003');
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static Page? GetForegroundPage()
    {
        Page? page = Application.Current?.MainPage;
        page = page?.Navigation.ModalStack.LastOrDefault() ?? page;
        while (page is not null)
        {
            var child = page switch
            {
                Shell shell => shell.CurrentPage,
                NavigationPage navigation => navigation.CurrentPage,
                TabbedPage tabs => tabs.CurrentPage,
                FlyoutPage flyout => flyout.Detail,
                _ => null
            };
            if (child is null || ReferenceEquals(child, page)) return page;
            page = child;
        }
        return null;
    }

    private sealed record Registration(Page Owner, Func<string, Task> Handle, Func<bool> CanScan);
}
