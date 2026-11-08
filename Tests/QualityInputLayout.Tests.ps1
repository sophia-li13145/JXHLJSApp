$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$page = Get-Content (Join-Path $root 'Pages/Quality/MachineQualityDetailPage.xaml.cs') -Raw
$method = [regex]::Match($page, '(?s)    private void ArrangeInspectionFields\(.*?(?=\r?\n    private )').Value
if (!$method) { throw 'Inspection input layout method not found' }
$support = @'
#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace QualityInputLayoutTests;
public class View { public Layout? Parent; public int Row, Column, Span = 1; }
public class Views : Collection<View>
{
    readonly Layout owner;
    public Views(Layout owner) { this.owner = owner; }
    protected override void InsertItem(int index, View view)
    {
        if (view.Parent != null) throw new Exception("Field already belongs to another layout");
        base.InsertItem(index, view); view.Parent = owner;
    }
    protected override void RemoveItem(int index) { this[index].Parent = null; base.RemoveItem(index); }
}
public class Layout : View { public Views Children; public Layout() { Children = new(this); } }
public enum GridLength { Auto }
public class RowDefinition { public GridLength Height; }
public class Grid : Layout
{
    public List<RowDefinition> RowDefinitions = new();
    public Grid(int count) { for (var i = 0; i < count; i++) Children.Add(new View()); }
    public static void SetColumnSpan(View field, int span) { field.Span = span; }
    // Match MAUI Grid.Add: placement resets the column span to one.
    public void Add(View field, int column, int row) { Children.Add(field); field.Column = column; field.Row = row; field.Span = 1; }
}
public class Page
{
    public Grid HeatTreatmentInputPanel = new(9), AcidInputPanel = new(12), ProcessInputPanel = new(13);
    public Layout InspectionInputFields = new();
    public View TorsionInputPanel = new(), InspectionResultPanel = new(), MemoInputPanel = new(), UnqualifiedDescriptionPanel = new();
'@
$cases = @'
    public static void Run()
    {
        var p = new Page();
        foreach (var mode in new[] { "heat", "heat", "acid", "process", "process", "acid", "heat" })
        {
            bool heat = mode == "heat", acid = mode == "acid";
            p.ArrangeInspectionFields(heat, acid);
            var panel = heat ? p.HeatTreatmentInputPanel : acid ? p.AcidInputPanel : p.ProcessInputPanel;
            int baseCount = heat ? 9 : acid ? 12 : 13;
            Check(panel.Children.Count == baseCount + (acid ? 3 : 4), "Refresh and process switches preserve field counts");
            int resultRow = (baseCount + (acid ? 0 : 1) + 1) / 2;
            Check(p.InspectionResultPanel.Parent == panel && p.InspectionResultPanel.Column == 0 && p.InspectionResultPanel.Row == resultRow && p.InspectionResultPanel.Span == 2 && p.MemoInputPanel.Row == resultRow + 1, "Inspection result occupies its own full-width row before memo");
            Check(p.MemoInputPanel.Parent == panel && p.UnqualifiedDescriptionPanel.Parent == panel && p.MemoInputPanel.Span == 2 && p.UnqualifiedDescriptionPanel.Span == 2 && p.MemoInputPanel.Column == 0 && p.UnqualifiedDescriptionPanel.Column == 0 && p.UnqualifiedDescriptionPanel.Row == p.MemoInputPanel.Row + 1, "Memo and unqualified explanation each occupy their own full-width row");
            Check(p.TorsionInputPanel.Parent == (acid ? p.InspectionInputFields : panel), "Acid excludes torsion while other processes retain it");
            Check(panel.RowDefinitions.Count == p.UnqualifiedDescriptionPanel.Row + 1, "Every visible field has an automatic-height row");
        }
        Console.WriteLine("PASS: " + checks + " quality input layout assertions");
    }
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
}
'@
Add-Type -TypeDefinition ($support + $method + $cases)
[QualityInputLayoutTests.Page]::Run()
