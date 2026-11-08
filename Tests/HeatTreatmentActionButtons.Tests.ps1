$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$page = Get-Content (Join-Path $root 'Pages/Quality/MachineQualityDetailPage.xaml.cs') -Raw
$method = [regex]::Match($page, '(?s)    private void ApplyActionButtons\(.*?(?=\r?\n    private )').Value
if (!$method) { throw 'Quality action button layout method not found' }
$support = @'
using System;
namespace HeatActionTests;
public record Thickness(int Horizontal, int Vertical);
public class Button { public string Text = ""; public bool IsVisible; public int Column, Span; }
public class Grid
{
    public Thickness Margin = new(0, 0);
    public static void SetColumn(Button button, int value) => button.Column = value;
    public static void SetColumnSpan(Button button, int value) => button.Span = value;
}
public class Page
{
    public Button SubmitButton = new(), CompleteButton = new();
    public Grid ActionBar = new();
'@
$cases = @'
    public static void Run()
    {
        var page = new Page();
        page.ApplyActionButtons(true, false, 5);
        Check(page.SubmitButton.IsVisible && !page.CompleteButton.IsVisible && page.SubmitButton.Span == 2, "Multiple remaining items show only full-width submit");
        Check(page.SubmitButton.Text == "提交质检" && page.CompleteButton.Text == "完成质检", "Heat treatment action labels");
        Check(page.ActionBar.Margin.Horizontal == 20, "Heat treatment buttons retain side spacing");
        page.ApplyActionButtons(true, false, 1);
        Check(!page.SubmitButton.IsVisible && page.CompleteButton.IsVisible && page.CompleteButton.Column == 0 && page.CompleteButton.Span == 2, "Last item shows only full-width completion starting at first column");
        page.ApplyActionButtons(true, false, 2);
        Check(page.SubmitButton.IsVisible && !page.CompleteButton.IsVisible && page.SubmitButton.Span == 2, "Refreshed quantity can switch completion back to submit");
        foreach (decimal? qty in new decimal?[] { 0, null })
        {
            page.ApplyActionButtons(true, false, qty);
            Check(!page.SubmitButton.IsVisible && !page.CompleteButton.IsVisible, "Zero or missing uninspected quantity hides both heat treatment actions");
        }
        page.ApplyActionButtons(false, false, 1);
        Check(page.SubmitButton.IsVisible && page.CompleteButton.IsVisible && page.SubmitButton.Text == "提交本次质检" && page.CompleteButton.Text == "完 成" && page.ActionBar.Margin.Horizontal == 6, "Other processes retain their two-button labels and spacing");
        page.ApplyActionButtons(false, true, 1);
        Check(page.SubmitButton.IsVisible && !page.CompleteButton.IsVisible && page.SubmitButton.Span == 2 && page.SubmitButton.Text == "提交质检", "Submit-only processes remain unchanged");
        Console.WriteLine("PASS: " + checks + " heat treatment action button assertions");
    }
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
}
'@
Add-Type -TypeDefinition ($support + $method + $cases)
[HeatActionTests.Page]::Run()
