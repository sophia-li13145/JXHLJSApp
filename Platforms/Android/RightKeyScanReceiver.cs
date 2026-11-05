using Android.Content;
using Android.Runtime;
using JXHLJSApp.Services;
using JXHLJSApp.Tools;
using Serilog;

namespace JXHLJSApp.Platforms.Android;

[Register("com.jxhljs.app.RightKeyScanReceiver")]
public sealed class RightKeyScanReceiver : BroadcastReceiver
{
    private readonly RightKeyScanService? _scanner;
    private readonly string _dataKey = RightKeyScanProtocol.DefaultDataKey;

    public RightKeyScanReceiver(RightKeyScanService scanner, string dataKey)
    {
        _scanner = scanner;
        _dataKey = dataKey;
    }

    private RightKeyScanReceiver(IntPtr handle, JniHandleOwnership transfer) : base(handle, transfer) { }

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent is null) return;
        try
        {
            var code = RightKeyScanProtocol.ReadResult(intent.GetStringExtra, _dataKey);
            Log.Information("Right-key scan broadcast. Action={Action}, Length={Length}", intent.Action, code?.Length ?? 0);
            _scanner?.Receive(code);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Cannot read right-key scan result");
        }
    }
}
