using System.Runtime.CompilerServices;
using Serilog;

namespace JXHLJSApp.Services;

public interface IRightKeyScanService
{
    event Action? StateChanged;
    int InputRevision { get; }
    bool IsReady(Page owner);
    void Submit(Page owner, int revision, string code);
    void Register(Page owner, Func<string, Task> handle, Func<bool>? canScan = null, Func<Entry?>? textEntry = null);
    void RefreshInput(Page owner);
    Task ShowHintAsync(Page owner, string target);
}

/// <summary>Routes device results and explicitly bound machine input to the foreground page.</summary>
public sealed class RightKeyScanService : IRightKeyScanService
{
    private readonly ConditionalWeakTable<Page, Registration> _pages = new();
    private bool _active;
    private bool _focused;
    private int _revision;
    private WeakReference<Page>? _lastPage;
    private string? _lastCode;
    private long _lastAt;

    public event Action? StateChanged;
    public int InputRevision => _revision;
    internal Page? TextOwner
    {
        get
        {
            var page = GetForegroundPage();
            return CanReceive(page, out _) ? page : null;
        }
    }
    internal Entry? TextEntry => GetForegroundPage() is { } page && _pages.TryGetValue(page, out var registration)
        ? registration.TextEntry?.Invoke() : null;
    internal bool CanReceiveText => TextOwner is not null;
    internal bool IsForeground => _active && _focused;
    public bool IsReady(Page owner) => ReferenceEquals(owner, GetForegroundPage()) && CanReceive(owner, out _);

    public void Submit(Page owner, int revision, string code)
    {
        if (revision == _revision && IsReady(owner)) Receive(code);
    }

    public void Register(Page owner, Func<string, Task> handle, Func<bool>? canScan = null, Func<Entry?>? textEntry = null)
    {
        var registration = new Registration(handle, canScan ?? (() => true), textEntry);
        _pages.Add(owner, registration);
        owner.Appearing += (_, _) => { registration.Visible = true; Invalidate(); };
        owner.Disappearing += (_, _) =>
        {
            registration.Visible = false;
            // 页面离开即结束本次扫码会话，旧异步回调不能阻塞返回后的页面。
            registration.Operation = null;
            Invalidate();
        };
    }

    public void RefreshInput(Page owner)
    {
        if (ReferenceEquals(owner, GetForegroundPage())) Invalidate();
    }

    public void SetActive(bool active)
    {
        _active = active;
        Invalidate();
    }

    public void SetWindowFocused(bool focused)
    {
        _focused = focused;
        Invalidate();
    }

    private void Invalidate()
    {
        _revision++;
        _lastCode = null;
        StateChanged?.Invoke();
    }

    public void Receive(string? rawCode)
    {
        // Capture eligibility at arrival, so a result received while busy is never replayed.
        var page = GetForegroundPage();
        if (!CanReceive(page, out var registration))
        {
            Log.Information("Scan result ignored. Page={Page}, Active={Active}, Focused={Focused}, Processing={Processing}, Dialog={Dialog}",
                page?.GetType().Name, _active, _focused, registration?.Operation is not null, ErrorDialogService.IsDialogVisible);
            return;
        }
        var code = rawCode?.Trim(' ', '\t', '\r', '\n', '\uFEFF', '\0', '\u0002', '\u0003');
        if (string.IsNullOrWhiteSpace(code)) return;
        var revision = _revision;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (revision != _revision || !ReferenceEquals(page, GetForegroundPage()) ||
                !CanReceive(page, out var current) || !ReferenceEquals(current, registration)) return;
            var now = Environment.TickCount64;
            if (_lastPage?.TryGetTarget(out var lastPage) == true && ReferenceEquals(lastPage, page) &&
                code == _lastCode && now - _lastAt < 500) return;
            _lastPage = new WeakReference<Page>(page!);
            _lastCode = code;
            _lastAt = now;
            var operation = new object();
            registration!.Operation = operation;
            _revision++; // Discard any other results queued against the preceding step.
            StateChanged?.Invoke();
            try
            {
                Log.Information("Right-key scan accepted. Page={Page}, Length={Length}", page!.GetType().Name, code.Length);
                await registration!.Handle(code);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Right-key scan failed. Page={Page}", page!.GetType().Name);
                if (ReferenceEquals(registration.Operation, operation) && ReferenceEquals(page, GetForegroundPage()))
                    await ErrorDialogService.ShowAsync(page, "扫码失败", ex.Message);
            }
            finally
            {
                // 旧请求晚结束时，不得清除同一页面新一轮扫码的处理状态。
                if (ReferenceEquals(registration.Operation, operation))
                {
                    registration.Operation = null;
                    StateChanged?.Invoke();
                }
            }
        });
    }

    private bool CanReceive(Page? page, out Registration? registration)
    {
        registration = null;
        return _active && _focused && !ErrorDialogService.IsDialogVisible &&
            page is not null && _pages.TryGetValue(page, out registration) &&
            registration.Visible && registration.Operation is null && registration.CanScan();
    }

    public Task ShowHintAsync(Page owner, string target)
    {
        // A tap explains the physical action without opening a modal or starting a scan session.
        if (ReferenceEquals(owner, GetForegroundPage()))
        {
#if ANDROID
            global::Android.Widget.Toast.MakeText(global::Android.App.Application.Context,
                $"{target}：请对准标签，按右侧扫描键。", global::Android.Widget.ToastLength.Short)?.Show();
#endif
        }
        return Task.CompletedTask;
    }

    private static Page? GetForegroundPage()
    {
        Page? page = Application.Current?.MainPage;
        // MAUI navigation proxies expose the same modal stack on modal children too.
        // Resolve it once to avoid walking from a modal child back into its container.
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

    private sealed class Registration(Func<string, Task> handle, Func<bool> canScan, Func<Entry?>? textEntry)
    {
        public Func<string, Task> Handle { get; } = handle;
        public Func<bool> CanScan { get; } = canScan;
        public Func<Entry?>? TextEntry { get; } = textEntry;
        public bool Visible { get; set; }
        public object? Operation { get; set; }
    }
}
