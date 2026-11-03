using Android.Content;
using Android.Runtime;
using JXHLJSApp.Services;
using JXHLJSApp.Tools;
using Serilog;

namespace JXHLJSApp.Platforms.Android;

[Register("com.jxhljs.app.HardwareScanReceiver")]
public sealed class HardwareScanReceiver : BroadcastReceiver
{
    private readonly HardwareScanService? _scanner;
    private readonly string _dataKey = "data";

    public HardwareScanReceiver(HardwareScanService scanner, string dataKey)
    {
        _scanner = scanner;
        _dataKey = dataKey;
    }

    private HardwareScanReceiver(IntPtr handle, JniHandleOwnership transfer) : base(handle, transfer) { }

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent is null) return;
        try
        {
            var code = HardwareScanProtocol.ReadResult(intent.GetStringExtra, _dataKey);
            Log.Information("Hardware scan broadcast. Action={Action}, State={State}, Length={Length}",
                intent.Action, intent.GetStringExtra("SCAN_STATE"), code?.Length ?? 0);
            _scanner?.Publish(code);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Cannot read hardware scan broadcast. Action={Action}", intent.Action);
        }
    }
}
