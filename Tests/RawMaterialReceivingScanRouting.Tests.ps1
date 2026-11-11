$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content (Join-Path $root 'Pages/Warehouse/AddRawMaterialReceivingPage.xaml.cs') -Raw
$routing = [regex]::Match($source, '(?s)    private Task HandleTicketScanAsync.*?(?=    private async Task ScanIncomingTicketAsync)').Value
$binding = [regex]::Match($source, '(?s)    private async Task ScanAndBindTicketAsync.*?(?=    private void ShowBindConfirmDialog)').Value
$duplicate = [regex]::Match($source, '(?s)    private async Task<bool> RejectDuplicatePendingMaterialAsync.*?(?=    private async void OnConfirmBindClicked)').Value
if (!$routing -or !$binding -or !$duplicate) { throw 'Scan routing methods not found' }
$support = @'
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
namespace RawMaterialScanRoutingTests;
public class Material { public string? qrCode; }
public class Scanner { public Task ShowHintAsync(object p, string s) => Task.CompletedTask; }
public class Api {
    public int Queries;
    public string? Sent, ReturnedCode;
    public bool Fail;
    public Task<Material> QueryQrCodeInfoAsync(string code) {
        Queries++; Sent = code;
        if (Fail) throw new InvalidOperationException("query failed");
        return Task.FromResult(new Material { qrCode = ReturnedCode ?? code });
    }
}
public static class ErrorDialogService {
    public static Task ShowAsync(Page p, string title, string message, string button) {
        p.Errors++; return Task.CompletedTask;
    }
}
public class Page {
    public bool _isHandlingScan, _isStashingInstock, _isSubmittingInstock, _isChangingWarehouseSelection;
    public object? _failedStashRequest;
    public string? _instockNo = "IN001";
    public Material? _selectedTicket = new();
    public List<Material> _ocrItems = new();
    public Api _warehouseApi = new();
    public Scanner _scanner = new();
    public int Tickets, Dialogs, Alerts, Errors;
    public string? TicketContent, BoundCode;
    public Task Scan(string code) => HandleTicketScanAsync(code);
    public static bool Matches(string code) => TryNormalizePendingMaterialCode(code, out _);
    private Task ScanIncomingTicketAsync(string code) { Tickets++; TicketContent = code; return Task.CompletedTask; }
    private void ShowBindConfirmDialog(string code, Material source) { Dialogs++; BoundCode = code; }
    private Task DisplayAlert(string t, string m, string b) { Alerts++; return Task.CompletedTask; }
'@
$cases = @'
}
public static class Tests {
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    public static async Task Run() {
        foreach (var prefix in new[] { "ZJ", "Y", "CP", "OTHER", "BCP" }) {
            foreach (var suffix in new[] { "202600000001", "199900000000", "202699999999" }) {
                var page = new Page();
                var code = prefix + suffix;
                await page.Scan(" \r\n" + code.ToLowerInvariant() + "\t");
                Check(page._warehouseApi.Sent == code && page.BoundCode == code, "Normalize complete material code: " + code);
                Check(page.Dialogs == 1 && page.Tickets == 0 && page._ocrItems.Count == 0, "Open confirmation without adding before confirmation");
                Check(!page._isHandlingScan, "Release scan lock after binding");
            }
        }
        foreach (var code in new[] {
            "ZJ20260000001", "Y2026000000001", "CP20260000000A", "OTHER2026 00000001",
            "BCP+2026+00000001", "XYZ202600000001", "https://example.test/ZJ202600000001",
            "{\"code\":\"ZJ202600000001\"}", "prefixZJ202600000001", "ZJ202600000001suffix",
            "ZJ２０２６０００００００１", "ZJ202600000001\nextra", "票签内容\r\n炉号：123"
        }) {
            var page = new Page();
            await page.Scan(code);
            Check(page.Tickets == 1 && page.TicketContent == code && page._warehouseApi.Queries == 0,
                "Nonmatching content remains unchanged for ticket recognition: " + code);
        }
        var noTicket = new Page { _selectedTicket = null };
        await noTicket.Scan("ZJ202600000001");
        Check(noTicket.Alerts == 1 && noTicket.Tickets == 0 && noTicket._warehouseApi.Queries == 0 && !noTicket._isHandlingScan,
            "Material code without a template prompts without ticket fallback");
        var duplicate = new Page();
        duplicate._ocrItems.Add(new Material { qrCode = " zj202600000001 " });
        await duplicate.Scan("ZJ202600000001");
        Check(duplicate.Alerts == 1 && duplicate._warehouseApi.Queries == 0 && duplicate.Dialogs == 0,
            "Duplicate rejected before querying");
        var alias = new Page();
        alias._warehouseApi.ReturnedCode = "EXISTING";
        alias._ocrItems.Add(new Material { qrCode = "EXISTING" });
        await alias.Scan("BCP202600000001");
        Check(alias.Alerts == 1 && alias.Dialogs == 0, "Backend canonical duplicate rejected");
        var failed = new Page();
        failed._warehouseApi.Fail = true;
        await failed.Scan("OTHER202600000001");
        Check(failed.Errors == 1 && failed.Tickets == 0 && failed.Dialogs == 0 && !failed._isHandlingScan,
            "Query failure never falls back to ticket recognition");
        foreach (var blocked in new[] {
            new Page { _isHandlingScan = true }, new Page { _isStashingInstock = true },
            new Page { _isSubmittingInstock = true }, new Page { _isChangingWarehouseSelection = true },
            new Page { _failedStashRequest = new object() }, new Page { _instockNo = null }
        }) {
            await blocked.Scan("ZJ202600000001"); await blocked.Scan("票签");
            Check(blocked._warehouseApi.Queries == 0 && blocked.Tickets == 0, "Busy or unsaved failure blocks both routes");
        }
        var empty = new Page();
        await empty.Scan(""); await empty.Scan(" \r\n");
        Check(empty.Tickets == 0 && empty._warehouseApi.Queries == 0, "Empty scans ignored");
        var consecutive = new Page();
        await consecutive.Scan("ZJ202600000001");
        await consecutive.Scan("新的票签内容");
        Check(consecutive.Dialogs == 1 && consecutive.Tickets == 1, "Selected ticket never forces subsequent ticket into binding");
        Console.WriteLine("PASS: " + checks + " raw material scan routing assertions");
    }
}
'@
Add-Type -TypeDefinition ($support + $routing + $binding + $duplicate + $cases)
$null = [RawMaterialScanRoutingTests.Tests]::Run().GetAwaiter().GetResult()
