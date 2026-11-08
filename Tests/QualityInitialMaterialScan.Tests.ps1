$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$detail = Get-Content (Join-Path $root 'Pages/Quality/MachineQualityDetailPage.xaml.cs') -Raw
$list = Get-Content (Join-Path $root 'Pages/Quality/ProductionQualityOrderListPage.xaml.cs') -Raw
$appearing = [regex]::Match($detail, '(?s)    protected override async void OnAppearing\(\).*?(?=\r?\n    private )').Value
$navigation = [regex]::Match($list, '(?s)    private async void OnOrderTapped\(.*?(?=\r?\n    private )').Value
$query = [regex]::Match($list, '(?s)    public void ApplyQueryAttributes\(.*?(?=\r?\n    protected )').Value
$initialCodeProperty = [regex]::Match($detail, '    public string\? InitialMaterialQrCode[^\r\n]+').Value
if (!$appearing -or !$navigation) { throw 'Quality navigation methods not found' }
$appearing = $appearing.Replace('protected override async void OnAppearing()', 'public async Task AppearAsync()')
$navigation = $navigation.Replace('private async void OnOrderTapped', 'public async Task OnOrderTapped')
$support = @'
#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace QualityInitialScanTests;
public class BasePage { protected void OnAppearing() {} }
public static class ErrorDialogService { public static bool IsDialogVisible; }
public class TappedEventArgs : EventArgs {}
public class BindableObject { public object? BindingContext; }
public class View { public bool IsVisible; }
public class ProductionQualityOrderDto
{
    public string? qualityNo, orderNumber, inspectStatus, inspectionSchemeName, qualityType, qualityTypeName;
}
public static class AppShell { public const string RouteMachineQualityDetail = "detail"; }
public class Shell
{
    public static Shell Current = new();
    public string Route = "";
    public Task GoToAsync(string route) { Route = route; return Task.CompletedTask; }
}
public class Detail : BasePage
{
    public bool _hasLoadedDetail, _canScanMaterial = true, FailLoad, ShowSuccess;
    public string? _loadedQualityNo, _qualityNo = "QUALITY", _initialMaterialQrCode, Received;
    public int Loads, Scans;
    private Task LoadAsync()
    {
        Loads++;
        _hasLoadedDetail = !FailLoad;
        _loadedQualityNo = _qualityNo;
        return Task.CompletedTask;
    }
    private Task ScanAndApplyMaterialAsync(string code, bool showSuccess = true)
    {
        Scans++; Received = code; ShowSuccess = showSuccess;
        return Task.CompletedTask;
    }
'@
$listSupport = @'
}
public class Orders
{
    public string? _qrCode;
    public View FilterPanel = new(), ManualInspectionPanel = new();
    private Task DisplayAlert(string title, string text, string button) => Task.CompletedTask;
'@
$cases = @'
}
public static class Tests
{
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    public static async Task Run()
    {
        var code = "物料%2F&A=1\nQR";
        var orders = new Orders { _qrCode = code };
        orders.ApplyQueryAttributes(new Dictionary<string, object> { ["qrCode"] = Uri.EscapeDataString(code) });
        Check(orders._qrCode == code, "List decodes URI query exactly once");
        var item = new ProductionQualityOrderDto { qualityNo = "Q1", orderNumber = "W1" };
        await orders.OnOrderTapped(new BindableObject { BindingContext = item }, new());
        Check(Shell.Current.Route.Contains("initialMaterialQrCode=" + Uri.EscapeDataString(code)), "Scanned QR survives list navigation with special characters");
        orders._qrCode = null;
        await orders.OnOrderTapped(new BindableObject { BindingContext = item }, new());
        Check(!Shell.Current.Route.Contains("initialMaterialQrCode"), "Ordinary list does not request automatic material scan");
        var page = new Detail { InitialMaterialQrCode = code };
        Check(page._initialMaterialQrCode == code, "QueryProperty does not decode the Shell-decoded code again");
        await page.AppearAsync();
        Check(page.Scans == 1 && page.Received == code && page.Loads == 1, "Detail loads then queries the original material code");
        Check(!page.ShowSuccess && page._initialMaterialQrCode == null, "Automatic query consumes code without success popup");
        await page.AppearAsync();
        Check(page.Scans == 1 && page.Loads == 1, "Reappearing does not query twice");
        page._hasLoadedDetail = false;
        await page.AppearAsync();
        Check(page.Scans == 1 && page.Loads == 2, "Detail refresh does not add the initial material twice");
        var ordinary = new Detail(); await ordinary.AppearAsync();
        Check(ordinary.Scans == 0, "No supplied code means no material request");
        var completed = new Detail { _initialMaterialQrCode = code, _canScanMaterial = false };
        await completed.AppearAsync();
        Check(completed.Scans == 0, "Completed or unsupported flow does not scan material");
        var failed = new Detail { _initialMaterialQrCode = code, FailLoad = true };
        await failed.AppearAsync();
        Check(failed.Scans == 0 && failed._initialMaterialQrCode == code, "Failed detail load preserves code without querying material");
        failed.FailLoad = false; await failed.AppearAsync();
        Check(failed.Scans == 1, "Successful retry uses the pending original code");
        ErrorDialogService.IsDialogVisible = true;
        var modal = new Detail { _initialMaterialQrCode = code }; await modal.AppearAsync();
        Check(modal.Scans == 0 && modal.Loads == 0, "Modal lifecycle does not trigger a backend operation");
        ErrorDialogService.IsDialogVisible = false;
        Console.WriteLine("PASS: " + checks + " initial quality material scan assertions");
    }
}
'@
Add-Type -TypeDefinition ($support + $initialCodeProperty + $appearing + $listSupport + $query + $navigation + $cases)
$null = [QualityInitialScanTests.Tests]::Run().GetAwaiter().GetResult()
