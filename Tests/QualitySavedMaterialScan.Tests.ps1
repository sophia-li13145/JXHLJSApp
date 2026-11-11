$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content (Join-Path $root 'Pages/Quality/MachineQualityDetailPage.xaml.cs') -Raw
$methods = [regex]::Match($source, '(?s)    private bool HasSavedResultForMaterial.*?(?=    private Task<ProductionQualityScanMaterialDto> ScanMaterialForCurrentFlowAsync)').Value
if (!$methods) { throw 'Material scan methods not found' }
$exception = Get-Content (Join-Path $root 'Services/Quality/QualityMaterialAlreadyInspectedException.cs') -Raw
$exception = $exception.Replace('namespace JXHLJSApp.Services.Quality;', '')
$support = @'
#nullable enable
using System;
using System.Threading.Tasks;
namespace QualitySavedMaterialTests;
public class View { public bool IsVisible; public string? Text; }
public class ProductionQualityDetailDto { public string? qrCode; public bool? resultSaved; }
public class ProductionQualityScanMaterialDto { public bool resultSaved; }
public class ProductionQualityScanMaterialRequestDto { public string? qrCode, qualityNo, workOrderNo; }
public class Scanner { public Task ShowHintAsync(object p, string s) => Task.CompletedTask; }
public static class ErrorDialogService
{
    public static int Errors;
    public static Task ShowAsync(object page, string title, string message, string button)
    { Errors++; return Task.CompletedTask; }
}
public class Page
{
    public ProductionQualityDetailDto? _detail;
    public bool _requiresUninspectedMaterial, _isHandlingScan, _isUpdating, _isManualInspection;
    public bool _hasLoadedDetail = true;
    public string? _qualityNo = "QUALITY", _workOrderNo = "WORK", _inspectStatus;
    public View MaterialScanStatusLabel = new(), ActionBar = new() { IsVisible = true },
        SubmitButton = new() { IsVisible = true }, CompleteButton = new();
    public Scanner _scanner = new();
    public int Scans, Refreshes, Applied, Captures;
    public string? Sent;
    public Exception? Failure;
    public bool ReturnedSaved;
    public Task Scan(string code) => ScanAndApplyMaterialAsync(code, showSuccess: false);
    private Task DisplayAlert(string t, string m, string b) => Task.CompletedTask;
    private bool ShouldPreserveManualInputsOnMaterialScan() => true;
    private object? CaptureManualInputState() { Captures++; return new object(); }
    private bool ShouldRefreshDetailAfterMaterialScan() => true;
    private Task RefreshDetailAfterMaterialScanAsync() { Refreshes++; return Task.CompletedTask; }
    private void RestoreManualInputState(object? s) {}
    private void ApplyPreviousUnqualifiedState(ProductionQualityScanMaterialDto m) {}
    private void UpdateAutoInspectionResult() {}
    private bool IsInspectionCompleted(string? s) => s == "3";
    private void ApplyScannedMaterial(ProductionQualityScanMaterialDto m, string code)
    { Applied++; _detail = new() { qrCode = code, resultSaved = m.resultSaved }; }
    private Task<ProductionQualityScanMaterialDto> ScanMaterialForCurrentFlowAsync(ProductionQualityScanMaterialRequestDto r)
    {
        Scans++; Sent = r.qrCode;
        if (Failure != null) throw Failure;
        return Task.FromResult(new ProductionQualityScanMaterialDto { resultSaved = ReturnedSaved });
    }
'@
$cases = @'
}
public static class Tests
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    public static async Task Run()
    {
        var code = "QR%2F&A=1";
        var saved = new Page { _detail = new() { qrCode = code, resultSaved = true } };
        await saved.Scan(code);
        Check(saved.Scans == 0 && saved.Refreshes == 0, "Same saved QR must not call scan or refresh");
        Check(saved._requiresUninspectedMaterial && !saved.ActionBar.IsVisible && saved.MaterialScanStatusLabel.IsVisible,
            "Saved material hides actions and shows status");
        Check(saved._detail.qrCode == code && saved._detail.resultSaved == true, "Saved result remains available");

        await saved.Scan("NEW");
        Check(saved.Scans == 1 && saved.Sent == "NEW" && saved.Applied == 1, "Different QR still scans despite saved detail");
        Check(!saved._requiresUninspectedMaterial && saved.ActionBar.IsVisible && !saved.MaterialScanStatusLabel.IsVisible,
            "New uninspected material restores actions");
        Check(saved.Captures == 0, "Do not copy saved inspection inputs into another material");

        foreach (bool? state in new bool?[] { null, false })
        {
            var unknown = new Page { _detail = new() { qrCode = code, resultSaved = state } };
            await unknown.Scan(code);
            Check(unknown.Scans == 1, "Unknown or unsaved state must still scan");
        }
        var escaped = new Page { _detail = new() { qrCode = "QR/A=1", resultSaved = true } };
        await escaped.Scan(code);
        Check(escaped.Scans == 1 && escaped.Sent == code, "QR text is compared and sent without extra URI decoding");

        const string duplicate = "该二维码已提交质检结果，请勿重复扫码";
        Check(QualityMaterialAlreadyInspectedException.IsAlreadyInspectedMessage(duplicate), "Recognizes known backend business response");
        Check(!QualityMaterialAlreadyInspectedException.IsAlreadyInspectedMessage("字典查询失败"), "Does not classify unrelated failures as saved");
        var rejected = new Page { _detail = new() { qrCode = "OLD" },
            Failure = new QualityMaterialAlreadyInspectedException(duplicate, new InvalidOperationException(duplicate)) };
        await rejected.Scan(code);
        Check(ErrorDialogService.Errors == 0 && rejected._requiresUninspectedMaterial && !rejected.ActionBar.IsVisible,
            "Duplicate business response shows status without scan failure dialog");
        Check(rejected._detail.qrCode == "OLD" && rejected.Applied == 0 && rejected.Refreshes == 0,
            "Rejected QR must not overwrite current material or borrow another material result");
        rejected.Failure = null;
        await rejected.Scan("NEXT");
        Check(!rejected._requiresUninspectedMaterial && rejected.ActionBar.IsVisible, "Can recover by scanning another material");

        var bad = new Page { Failure = new InvalidOperationException("物料不存在") };
        await bad.Scan(code);
        Check(ErrorDialogService.Errors == 1 && !bad._isHandlingScan, "Other failures remain errors and release scan lock");
        var returnedSaved = new Page { ReturnedSaved = true };
        await returnedSaved.Scan(code);
        Check(returnedSaved._requiresUninspectedMaterial && !returnedSaved.ActionBar.IsVisible,
            "Successful response explicitly marked saved cannot be submitted again");
        var updating = new Page { _isUpdating = true };
        await updating.Scan(code);
        Check(updating.Scans == 0, "Cannot change material during submission");
        Console.WriteLine("PASS: " + checks + " saved material scan assertions");
    }
}
'@
Add-Type -TypeDefinition ($support + $methods + $cases + $exception)
$null = [QualitySavedMaterialTests.Tests]::Run().GetAwaiter().GetResult()
