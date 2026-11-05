$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$pageSource = Get-Content (Join-Path $root 'Pages/Warehouse/AddRawMaterialReceivingPage.xaml.cs') -Raw
function Get-Methods([string] $start, [string] $end) {
    $from = $pageSource.IndexOf($start)
    $to = $pageSource.IndexOf($end, $from)
    if ($from -lt 0 -or $to -lt 0) { throw "Cannot locate production methods: $start" }
    return $pageSource.Substring($from, $to - $from)
}
$recognition = Get-Methods '    private async void OnScanIncomingClicked(' '    private void SetLoadingState('
$binding = Get-Methods '    private async void OnScanBindClicked(' '    private void ShowBindConfirmDialog('
$support = @'
public class RawMaterialOcrDto { public string? ocrRawText; }
public class QrInfo { public string? qrCode; }
public class FakeApi
{
    public string? Content, Instock;
    public int Calls, BindCalls;
    public Func<Task<RawMaterialOcrDto>>? Recognize;
    public Task<RawMaterialOcrDto> ScanIncomingAsync(string content, string instock)
    { Calls++; Content = content; Instock = instock; return Recognize?.Invoke() ?? Task.FromResult(new RawMaterialOcrDto()); }
    public Task<QrInfo> QueryQrCodeInfoAsync(string code)
    { BindCalls++; return Task.FromResult(new QrInfo { qrCode = code }); }
}
public class FakeScanner
{
    public void RefreshInput(object owner) { }
    public Task ShowHintAsync(object owner, string target) => Task.CompletedTask;
}
public static class Log { public static void Error(Exception ex, string message, params object?[] args) { } }
public static class ErrorDialogService
{
    public static int Errors;
    public static Task ShowAsync(object page, string title, string message, string confirm)
    { Errors++; return Task.CompletedTask; }
}
public partial class Page
{
    private readonly FakeApi _warehouseApi = new();
    private readonly FakeScanner _scanner = new();
    private bool _isHandlingScan, _hasPendingTicketScan, _scanForRecognition = true;
    private string? _instockNo = "IN-100";
    private RawMaterialOcrDto? _selectedTicket;
    public RawMaterialOcrDto? Confirmation;
    public bool Loading, FailedHint;
    public string? Binding;
    private void SetLoadingState(bool loading, string? message = null) { Loading = loading; }
    private void ShowTicketConfirmDialog(RawMaterialOcrDto ticket, bool failed = false)
    { Confirmation = ticket; FailedHint = failed; }
    private void ShowBindConfirmDialog(string code, RawMaterialOcrDto ticket) { Binding = code; }
    private Task DisplayAlert(string title, string text, string confirm) => Task.CompletedTask;
    public static async Task Run()
    {
        var page = new Page();
        var original = "{\"coil\":\"001\",\n\"weight\":12.5}\u001Dtail";
        await page.HandleTicketScanAsync(original);
        Check(page._warehouseApi.Content == original && page._warehouseApi.Instock == "IN-100",
            "Recognition passes complete QR payload and current instock number");
        Check(page.Confirmation != null && page._hasPendingTicketScan && !page.Loading && !page._isHandlingScan,
            "Recognition opens confirmation and enables saving without an attachment");
        page._selectedTicket = page.Confirmation;
        page.OnScanBindClicked(page, EventArgs.Empty);
        await page.HandleTicketScanAsync("TRACE-1");
        Check(page.Binding == "TRACE-1" && page._warehouseApi.BindCalls == 1 && page._warehouseApi.Calls == 1,
            "Binding mode keeps the original QR lookup and binding confirmation");
        page.OnScanIncomingClicked(page, EventArgs.Empty);
        await page.HandleTicketScanAsync("SECOND");
        Check(page._warehouseApi.Content == "SECOND" && page._warehouseApi.Calls == 2,
            "Recognition button switches an already selected ticket back to incoming recognition");
        var pending = new TaskCompletionSource<RawMaterialOcrDto>();
        page._warehouseApi.Recognize = () => pending.Task;
        var inFlight = page.HandleTicketScanAsync("WAIT");
        await page.HandleTicketScanAsync("DUPLICATE");
        Check(page._warehouseApi.Calls == 3 && page.Loading, "Scans while recognizing are discarded");
        pending.SetResult(new RawMaterialOcrDto()); await inFlight;
        page._warehouseApi.Recognize = () => Task.FromException<RawMaterialOcrDto>(new Exception("Failed"));
        await page.HandleTicketScanAsync(original);
        Check(ErrorDialogService.Errors == 1 && page.FailedHint && page.Confirmation?.ocrRawText == original
            && page._hasPendingTicketScan && !page.Loading && !page._isHandlingScan,
            "Failure retains manual correction with original payload and restores input state");
        var calls = page._warehouseApi.Calls;
        await page.HandleTicketScanAsync("  ");
        page._instockNo = null; await page.HandleTicketScanAsync("NO-ORDER");
        Check(page._warehouseApi.Calls == calls, "Empty scans and missing instock number do not send requests");
        Console.WriteLine("PASS: " + checks + " incoming ticket scan assertions");
    }
    private static int checks;
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
}
'@
$source = "#nullable enable`nusing System; using System.Threading.Tasks; namespace IncomingTicketScanTests {" +
    $support + 'public partial class Page {' + $recognition + $binding + '}}'
Add-Type -TypeDefinition $source
$null = [IncomingTicketScanTests.Page]::Run().GetAwaiter().GetResult()
