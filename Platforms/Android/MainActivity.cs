using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Content;
using JXHLJSApp.Services;
using JXHLJSApp.Platforms.Android;
using JXHLJSApp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace JXHLJSApp;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize
        | ConfigChanges.Orientation
        | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize
        | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private RightKeyScanReceiver? _scanReceiver;
    private RightKeyScanService? _scanner;
    private RightKeyScanInput? _scanInput;

    public bool IsAutomaticScanInput(Entry entry) => _scanInput?.IsAutomaticEntry(entry) == true;

    protected override void OnResume()
    {
        base.OnResume();
        var services = IPlatformApplication.Current?.Services;
        _scanner = services?.GetRequiredService<RightKeyScanService>();
        if (_scanner is null) return;
        _scanInput ??= new RightKeyScanInput(this, _scanner);
        _scanner.SetActive(true);
        _scanner.SetWindowFocused(HasWindowFocus);
        if (_scanReceiver is not null) return;

        try
        {
            var config = services!.GetRequiredService<IConfigLoader>().Load();
            var legacyAction = config["hardwareScanner"]?["broadcastAction"]?.GetValue<string>();
            var action = config["rightKeyScanner"]?["broadcastAction"]?.GetValue<string>() ?? RightKeyScanProtocol.DefaultAction;
            var dataKey = config["rightKeyScanner"]?["dataKey"]?.GetValue<string>()
                ?? config["hardwareScanner"]?["dataKey"]?.GetValue<string>() ?? RightKeyScanProtocol.DefaultDataKey;
            var receiver = new RightKeyScanReceiver(_scanner, dataKey);
            var filter = new IntentFilter();
            foreach (var acceptedAction in RightKeyScanProtocol.GetActions(action, legacyAction))
                filter.AddAction(acceptedAction);
            filter.AddCategory(Intent.CategoryDefault);
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
                RegisterReceiver(receiver, filter, ReceiverFlags.Exported);
            else
                RegisterReceiver(receiver, filter);
            _scanReceiver = receiver;
            Log.Information("Right-key scanner listening. Revision=unified-scan-20261104, Actions={Actions}, DataKey={DataKey}, Model={Model}",
                string.Join(",", RightKeyScanProtocol.GetActions(action, legacyAction)), dataKey, Build.Model);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Cannot register right-key scanner receiver");
        }
    }

    protected override void OnPause()
    {
        _scanner?.SetActive(false);
        if (_scanReceiver is not null)
        {
            try { UnregisterReceiver(_scanReceiver); }
            catch (Exception ex) { Log.Warning(ex, "Cannot unregister right-key scanner receiver"); }
            finally { _scanReceiver.Dispose(); _scanReceiver = null; }
        }
        base.OnPause();
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        // Native alerts and system pickers must not send scans to the page behind them.
        _scanner?.SetWindowFocused(hasFocus);
    }

    protected override void OnDestroy()
    {
        _scanInput?.Dispose();
        _scanInput = null;
        base.OnDestroy();
    }

}
