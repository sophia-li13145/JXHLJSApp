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
    public static void SetColumn(View field, int column) { field.Column = column; }
    public static void SetRow(View field, int row) { field.Row = row; }
    // Match MAUI Grid.Add: placement resets the column span to one.
    public void Add(View field, int column, int row) { Children.Add(field); field.Column = column; field.Row = row; field.Span = 1; }
}
public class Page
{
    public Grid HeatTreatmentInputPanel = new(9), AcidInputPanel = new(12), ProcessInputPanel = new(13);
    public Layout InspectionInputFields = new();
    public View TorsionInputPanel = new(), InspectionResultPanel = new(), MemoInputPanel = new(), UnqualifiedDescriptionPanel = new();
    public View StrengthInputPanel = new();
    public View CoilDiameterInputPanel, CoilDiameterDescriptionPanel, CoilPitchInputPanel, CoilPitchDescriptionPanel,
        ActualDiameterInputPanel, DiameterJudgmentPanel, StrengthJudgmentPanel, ElongationInputPanel,
        SurfaceInputPanel, SurfaceJudgmentPanel, ContinuousUnqualifiedPanel, EmployeeInterventionPanel;
    public Page()
    {
        ProcessInputPanel.Children.RemoveAt(6);
        ProcessInputPanel.Children.Insert(6, StrengthInputPanel);
        CoilDiameterInputPanel = ProcessInputPanel.Children[0];
        CoilDiameterDescriptionPanel = ProcessInputPanel.Children[1];
        CoilPitchInputPanel = ProcessInputPanel.Children[2];
        CoilPitchDescriptionPanel = ProcessInputPanel.Children[3];
        ActualDiameterInputPanel = ProcessInputPanel.Children[4];
        DiameterJudgmentPanel = ProcessInputPanel.Children[5];
        StrengthJudgmentPanel = ProcessInputPanel.Children[7];
        ElongationInputPanel = ProcessInputPanel.Children[8];
        SurfaceInputPanel = ProcessInputPanel.Children[9];
        SurfaceJudgmentPanel = ProcessInputPanel.Children[10];
        ContinuousUnqualifiedPanel = ProcessInputPanel.Children[11];
        EmployeeInterventionPanel = ProcessInputPanel.Children[12];
    }
'@
$cases = @'
    public static void Run()
    {
        var p = new Page();
        var originalProcessFields = new List<View>(p.ProcessInputPanel.Children);
        foreach (var mode in new[] { "heat", "heat", "drawing", "drawing", "acid", "process", "blank", "blank", "drawing", "process", "blank", "acid", "heat", "drawing", "process" })
        {
            bool heat = mode == "heat", acid = mode == "acid";
            bool blank = mode == "blank";
            p.ArrangeInspectionFields(heat, acid, blank, mode == "drawing");
            if (mode == "drawing")
            {
                var order = new[] { 4, 5, 6, 7, -1, 0, 1, 2, 3, 10, 9, 8, -2, 12, 11, -3, -4 };
                Check(p.ProcessInputPanel.Children.Count == 17, "Drawing retains all 17 fields after refresh or process switch");
                int row = 0, column = 0;
                for (int i = 0; i < order.Length; i++)
                {
                    var field = order[i] >= 0 ? originalProcessFields[order[i]] : order[i] == -1 ? p.TorsionInputPanel :
                        order[i] == -2 ? p.InspectionResultPanel : order[i] == -3 ? p.UnqualifiedDescriptionPanel : p.MemoInputPanel;
                    bool fullWidth = order[i] <= -2;
                    if (fullWidth && column != 0) { row++; column = 0; }
                    Check(p.ProcessInputPanel.Children[i] == field && field.Parent == p.ProcessInputPanel &&
                        field.Row == row && field.Column == column && field.Span == (fullWidth ? 2 : 1),
                        "Drawing uses the requested reading order and full-width result, explanation and memo");
                    if (fullWidth || column == 1) { row++; column = 0; } else column = 1;
                }
                Check(p.ProcessInputPanel.RowDefinitions.Count == row, "Drawing defines every required automatic-height row");
                continue;
            }
            var panel = heat ? p.HeatTreatmentInputPanel : acid ? p.AcidInputPanel : p.ProcessInputPanel;
            int baseCount = heat ? 9 : acid ? 12 : 13;
            Check(panel.Children.Count == baseCount + (acid ? 3 : 4), "Refresh and process switches preserve field counts");
            int resultRow = (baseCount + (acid ? 0 : 1) + 1) / 2;
            Check(p.InspectionResultPanel.Parent == panel && p.InspectionResultPanel.Column == 0 && p.InspectionResultPanel.Row == resultRow && p.InspectionResultPanel.Span == 2 && p.MemoInputPanel.Row == resultRow + 1, "Inspection result occupies its own full-width row before memo");
            Check(p.MemoInputPanel.Parent == panel && p.UnqualifiedDescriptionPanel.Parent == panel && p.MemoInputPanel.Span == 2 && p.UnqualifiedDescriptionPanel.Span == 2 && p.MemoInputPanel.Column == 0 && p.UnqualifiedDescriptionPanel.Column == 0 && p.UnqualifiedDescriptionPanel.Row == p.MemoInputPanel.Row + 1, "Memo and unqualified explanation each occupy their own full-width row");
            Check(p.TorsionInputPanel.Parent == (acid ? p.InspectionInputFields : panel), "Acid excludes torsion while other processes retain it");
            Check(panel.RowDefinitions.Count == p.UnqualifiedDescriptionPanel.Row + 1, "Every visible field has an automatic-height row");
            var expected = new List<View>(originalProcessFields);
            if (blank) expected.Remove(p.StrengthInputPanel);
            if (!heat && !acid) expected.Add(p.TorsionInputPanel);
            if (blank) expected.Add(p.StrengthInputPanel);
            for (var i = 0; i < expected.Count; i++)
                Check(p.ProcessInputPanel.Children[i] == expected[i] && expected[i].Row == i / 2 && expected[i].Column == i % 2,
                    "Process fields compact in order and refresh restores their positions");
            if (blank)
                Check(p.StrengthInputPanel.Row == p.InspectionResultPanel.Row - 1 && p.StrengthInputPanel.Column == 1 &&
                    p.TorsionInputPanel.Row == p.StrengthInputPanel.Row && p.TorsionInputPanel.Column == 0,
                    "Blank opening places strength after torsion as the last control before result");
        }
        Console.WriteLine("PASS: " + checks + " quality input layout assertions");
    }
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
}
'@
Add-Type -TypeDefinition ($support + $method + $cases)
[QualityInputLayoutTests.Page]::Run()
