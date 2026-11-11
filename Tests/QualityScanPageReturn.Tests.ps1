$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content (Join-Path $root 'Pages/Quality/ProductionQualityScanPage.xaml.cs') -Raw
$source = $source.Replace('using JXHLJSApp.Services;', '').Replace('namespace JXHLJSApp.Pages.Quality;', '')
$support = @'
#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace QualityScanReturnTests;
public class ContentPage
{
    protected virtual void OnAppearing() {}
    public void Appear() => OnAppearing();
    protected void InitializeComponent() {}
}
public class TappedEventArgs : EventArgs {}
public interface IRightKeyScanService
{
    void Register(ContentPage p, Func<string, Task> handle, Func<bool> canScan);
    void RefreshInput(ContentPage p);
    Task ShowHintAsync(ContentPage p, string text);
}
public class Scanner : IRightKeyScanService
{
    public Func<string, Task> Handle = null!;
    public Func<bool> CanScan = null!;
    public int Refreshes;
    public void Register(ContentPage p, Func<string, Task> handle, Func<bool> canScan)
    { Handle = handle; CanScan = canScan; }
    public void RefreshInput(ContentPage p) => Refreshes++;
    public Task ShowHintAsync(ContentPage p, string text) => Task.CompletedTask;
}
public static class AppShell { public const string RouteProductionQualityOrders = "orders"; }
public class Shell
{
    public static Shell Current = new();
    public List<string> Routes = new();
    public Queue<Task> Pending = new();
    public Task GoToAsync(string route) { Routes.Add(route); return Pending.Dequeue(); }
}
public static class ErrorDialogService
{
    public static int Errors;
    public static Task ShowAsync(ContentPage p, string t, string m, string b)
    { Errors++; return Task.CompletedTask; }
}
'@
$cases = @'
public static class Tests
{
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    public static async Task Run()
    {
        var scanner = new Scanner();
        var page = new ProductionQualityScanPage(scanner);
        var first = new TaskCompletionSource();
        var second = new TaskCompletionSource();
        Shell.Current.Pending.Enqueue(first.Task);
        Shell.Current.Pending.Enqueue(second.Task);
        page.Appear();
        var firstCall = scanner.Handle("QR%2F&A=1");
        Check(!scanner.CanScan(), "Navigation in progress blocks duplicate scans on the same session");
        await scanner.Handle("IGNORE");
        Check(Shell.Current.Routes.Count == 1, "Busy session must navigate only once");
        page.Appear();
        Check(scanner.CanScan() && scanner.Refreshes == 2, "Returning to the scan page resets its session and refreshes input");
        var secondCall = scanner.Handle("SECOND");
        Check(Shell.Current.Routes.Count == 2 && !scanner.CanScan(), "Returned page accepts a new scan before old navigation ends");
        first.SetResult(); await firstCall;
        Check(!scanner.CanScan(), "Old navigation completion must not clear the newer session lock");
        Check(Shell.Current.Routes[0] == "orders?qrCode=" + Uri.EscapeDataString("QR%2F&A=1"), "QR special characters remain encoded once");
        second.SetResult(); await secondCall;
        Check(scanner.CanScan(), "New navigation completion releases its own lock");

        var oldFailure = new TaskCompletionSource();
        Shell.Current.Pending.Enqueue(oldFailure.Task);
        var failedCall = scanner.Handle("OLD-FAILURE");
        page.Appear();
        oldFailure.SetException(new InvalidOperationException("late navigation failure"));
        await failedCall;
        Check(ErrorDialogService.Errors == 0 && scanner.CanScan(), "Old session failure cannot show an error on the returned page");
        Shell.Current.Pending.Enqueue(Task.FromException(new InvalidOperationException("current failure")));
        await scanner.Handle("FAIL");
        Check(ErrorDialogService.Errors == 1 && scanner.CanScan(), "Current session failure is reported and scanning recovers");
        Console.WriteLine("PASS: " + checks + " quality scan page return assertions");
    }
}
'@
Add-Type -TypeDefinition ($support + $source + $cases)
$null = [QualityScanReturnTests.Tests]::Run().GetAwaiter().GetResult()
