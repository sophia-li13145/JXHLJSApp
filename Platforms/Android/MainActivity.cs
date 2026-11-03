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
    private HardwareScanReceiver? _scanReceiver;
    private HardwareScanService? _hardwareScanner;
    private HardwareScanInput? _scanInput;

    protected override void OnResume()
    {
        base.OnResume();
        var services = IPlatformApplication.Current?.Services;
        _hardwareScanner = services?.GetRequiredService<HardwareScanService>();
        if (_hardwareScanner is null) return;
        _scanInput ??= new HardwareScanInput(this, _hardwareScanner);
        _hardwareScanner.SetForeground(true);
        if (_scanReceiver is not null) return;

        try
        {
            var config = services!.GetRequiredService<IConfigLoader>().Load();
            var action = config["hardwareScanner"]?["broadcastAction"]?.GetValue<string>() ?? "lc";
            var dataKey = config["hardwareScanner"]?["dataKey"]?.GetValue<string>() ?? "data";
            var receiver = new HardwareScanReceiver(_hardwareScanner, dataKey);
            var filter = new IntentFilter();
            foreach (var acceptedAction in HardwareScanProtocol.GetActions(action))
                filter.AddAction(acceptedAction);
            filter.AddCategory(Intent.CategoryDefault);
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
                RegisterReceiver(receiver, filter, ReceiverFlags.Exported);
            else
                RegisterReceiver(receiver, filter);
            _scanReceiver = receiver;
            Log.Information("Hardware scanner listening. Revision=newland-scan-20261102, Actions={Actions}, DataKey={DataKey}, Manufacturer={Manufacturer}, Model={Model}",
                string.Join(",", HardwareScanProtocol.GetActions(action)), dataKey, Build.Manufacturer, Build.Model);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Cannot register hardware scanner receiver");
        }
    }

    protected override void OnPause()
    {
        _hardwareScanner?.SetForeground(false);
        if (_scanReceiver is not null)
        {
            try { UnregisterReceiver(_scanReceiver); }
            catch (Exception ex) { Log.Warning(ex, "Cannot unregister hardware scanner receiver"); }
            finally { _scanReceiver.Dispose(); _scanReceiver = null; }
        }
        base.OnPause();
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        // Native alerts and system pickers must not send scans to the page behind them.
        _hardwareScanner?.SetWindowFocused(hasFocus);
    }

    protected override void OnDestroy()
    {
        _scanInput?.Dispose();
        _scanInput = null;
        base.OnDestroy();
    }
}
