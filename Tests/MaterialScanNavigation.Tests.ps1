$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
function Read-ScanMethod($path) {
    $source = Get-Content (Join-Path $root $path) -Raw
    $method = [regex]::Match($source, '(?s)    private async Task ScanMaterialAsync\(.*?(?=\r?\n    private |\r?\n\}\s*$)').Value
    if (!$method) { throw "Material scan method not found: $path" }
    $method.Replace('private async Task ScanMaterialAsync', 'public async Task ScanMaterialAsync')
}
$loading = Read-ScanMethod 'Pages/WorkStart/MaterialLoadingPage.xaml.cs'
$unloading = Read-ScanMethod 'Pages/WorkStart/MaterialUnloadingPage.xaml.cs'
$support = @'
public class View { public bool IsVisible = true; }
public class Material { public string? materialCode = "MAT"; }
public class Output { }
public class Context { public string WorkOrderNo = "ORDER"; }
public class ContextService { public Context Current = new(); }
public class CameraScanner
{
    public TaskCompletionSource<string?>? Pending;
    public Task<string?> ScanAsync(object page, string target) =>
        Pending?.Task ?? Task.FromResult<string?>(null);
}
public class Api
{
    public bool Fail, Empty;
    public Task<Material> ScanQueryMaterialInfoAsync(string code)
    {
        if (Fail) throw new InvalidOperationException("scan failed");
        return Task.FromResult(new Material { materialCode = Empty ? null : "MAT" });
    }
    public Task<List<Output>> GetWorkOrderInputOutputAsync(string order, string code)
    {
        if (Fail) throw new InvalidOperationException("scan failed");
        return Task.FromResult(Empty ? new List<Output>() : new List<Output> { new() });
    }
}
public static class ErrorDialogService
{
    public static TaskCompletionSource? Pending;
    public static Task ShowAsync(object owner, string title, string message, string button)
    {
        Pending = new TaskCompletionSource();
        return Pending.Task;
    }
    public static void Close() { var pending = Pending!; Pending = null; pending.SetResult(); }
}
public static class AppShell { public const string RouteMaterialUnloadingDetail = "detail"; }
public class Shell
{
    public static Shell Current = new();
    public int Navigations;
    public Dictionary<string, object>? Parameters;
    public Task GoToAsync(string route, Dictionary<string, object> parameters)
    {
        Navigations++; Parameters = parameters; return Task.CompletedTask;
    }
}
public class ScanState
{
    protected readonly Api _workOrderApi = new();
    protected readonly CameraScanner _cameraScanner = new();
    protected readonly ContextService _productionContext = new();
    protected bool _isHandlingScan;
    public Api Api => _workOrderApi;
    public CameraScanner Camera => _cameraScanner;
    public bool Busy => _isHandlingScan;
}
public partial class Loading : ScanState
{
    private bool _isBusy;
    private string? _lastMaterialQrCode;
    private Material? _confirmInput;
    public View ScanPanel = new(), SuccessBanner = new() { IsVisible = false }, ResultCard = new() { IsVisible = false };
    public bool BindFails;
    public bool CanRetry => !_isHandlingScan && !_isBusy && _confirmInput is null;
    private void BindMaterial(Material material)
    {
        _confirmInput = material;
        if (BindFails) throw new InvalidOperationException("binding failed");
    }
    private void ShowResultStep() { ScanPanel.IsVisible = false; SuccessBanner.IsVisible = ResultCard.IsVisible = true; }
    private void ShowMaterialScanStep() { }
}
public static class Regression
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        checks++; if (!condition) throw new Exception(message);
    }
    public static async Task Run()
    {
        var dualScan = new Loading();
        var camera = new TaskCompletionSource<string?>();
        dualScan.Camera.Pending = camera;
        var cameraScan = dualScan.ScanMaterialAsync();
        await dualScan.ScanMaterialAsync("HARDWARE");
        Check(dualScan.Busy && !dualScan.ResultCard.IsVisible, "Hardware results cannot interrupt the open camera");
        camera.SetResult(null);
        await cameraScan;
        await dualScan.ScanMaterialAsync("HARDWARE");
        Check(dualScan.ResultCard.IsVisible, "Hardware scanning resumes after camera cancellation");

        foreach (var failure in new[] { "api", "empty", "binding" })
        {
            var page = new Loading();
            page.Api.Fail = failure == "api"; page.Api.Empty = failure == "empty"; page.BindFails = failure == "binding";
            var scan = page.ScanMaterialAsync("BAD");
            Check(!scan.IsCompleted && ErrorDialogService.Pending is not null, "Loading failure waits for dialog close");
            ErrorDialogService.Close(); await scan;
            Check(page.CanRetry && page.ScanPanel.IsVisible && !page.ResultCard.IsVisible && !page.SuccessBanner.IsVisible,
                "Loading failure restores material scanning and clears partial binding");
            page.Api.Fail = page.Api.Empty = page.BindFails = false;
            await page.ScanMaterialAsync("GOOD");
            Check(page.ResultCard.IsVisible && !page.ScanPanel.IsVisible, "Loading accepts a new scan after failure");
        }
        foreach (var empty in new[] { false, true })
        {
            Shell.Current = new Shell();
            var page = new Unloading(); page.Api.Fail = !empty; page.Api.Empty = empty;
            var scan = page.ScanMaterialAsync("BAD");
            Check(!scan.IsCompleted && Shell.Current.Navigations == 0, "Unloading must query before navigating");
            ErrorDialogService.Close(); await scan;
            Check(!page.Busy && Shell.Current.Navigations == 0, "Closing failure dialog stays on unloading scanner");
            page.Api.Fail = page.Api.Empty = false;
            await page.ScanMaterialAsync(" GOOD ");
            Check(Shell.Current.Navigations == 1 && Shell.Current.Parameters!["inputOutput"] is Output
                && (string)Shell.Current.Parameters["qrCode"] == "GOOD", "Retry navigates with fetched detail and trimmed QR code");
        }
        Console.WriteLine("PASS: " + checks + " material scan navigation assertions");
    }
}
'@
$source = '#nullable enable' + [Environment]::NewLine +
    'using System; using System.Collections.Generic; using System.Linq; using System.Threading.Tasks; namespace MaterialScanTests {' +
    $support + 'public partial class Loading {' + $loading + '}' +
    'public class Unloading : ScanState { private bool _isBusy = false, _isRedirectingToDetail = false;' + $unloading + '}}'
Add-Type -TypeDefinition $source
$null = [MaterialScanTests.Regression]::Run().GetAwaiter().GetResult()
