$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$rule = Get-Content (Join-Path $root 'Services/Quality/ProductionQualityAutoResult.cs') -Raw
$rule = $rule.Replace('namespace JXHLJSApp.Services.Quality;', 'namespace QualityAutoTests;')
$page = Get-Content (Join-Path $root 'Pages/Quality/MachineQualityDetailPage.xaml.cs') -Raw
$update = [regex]::Match($page, '(?s)    private void UpdateAutoInspectionResult\(.*?(?=\r?\n    private )').Value.Replace('private void UpdateAutoInspectionResult', 'public void UpdateAutoInspectionResult')
$changed = [regex]::Match($page, '(?s)    private void OnInspectResultChanged\(.*?(?=\r?\n    private )').Value
if (!$update -or !$changed) { throw 'Automatic result UI methods not found' }
$ui = @'
public class Entry { public string? Text = "10"; }
public class Panel { public bool IsVisible; }
public class Picker
{
    public string? SelectedItem = "合格";
    int index;
    public event EventHandler? SelectedIndexChanged;
    public int SelectedIndex { get => index; set { if (index == value) return; index = value; SelectedIndexChanged?.Invoke(this, EventArgs.Empty); } }
}
public class Detail
{
    public string? lowerToleranceValue = "-0.1", upperToleranceValue = "0.2", strengthRequirement = "400-600";
    public string Process = "拉拔";
}
public class Page
{
    public bool _hasLoadedDetail = true, _isHandlingScan, _isUpdating;
    public Detail? _detail = new();
    public string? _inspectStatus;
    public Entry ActualDiameterEntry = new(), HeatActualDiameterEntry = new(), StrengthEntry = new() { Text = "700" }, TensileStrengthEntry = new() { Text = "700" };
    public Panel HeatTreatmentInputPanel = new();
    public Picker SurfaceJudgmentPicker = new() { SelectedItem = "不合格" }, CoilDiameterPicker = new(), CoilPitchPicker = new(), InspectResultPicker = new();
    public Page() { InspectResultPicker.SelectedIndexChanged += (sender, e) => OnInspectResultChanged(sender!, e); }
    bool IsInspectionCompleted(string? status) => status == "3";
    bool IsPicklingScheme(string flow) => flow == "酸洗";
    string ResolveCurrentFlowName(Detail detail) => detail.Process;
    string ResolveProductDiameter(Detail detail) => "10";
    void UpdateUnqualifiedDescriptionVisibility() {}
    void UpdateContinuousUnqualifiedSelection() {}
'@
$ui += $update + $changed + "}`n"
$tests = @'
public static class Tests
{
    public static void Run()
    {
        bool Evaluate(bool acid = false, string? diameter = "10", string? lower = "-0.1", string? upper = "0.2",
            string? actual = "10", string? range = "400-600", string? strength = "700",
            string? surface = "不合格", string? coil = "合格", string? pitch = "合格") =>
            ProductionQualityAutoResult.ShouldQualify(acid, diameter, lower, upper, actual, range, strength, surface, coil, pitch);
        Check(Evaluate(), "All five confirmed business conditions qualify");
        Check(!Evaluate(acid: true), "Acid never uses this automatic rule");
        Check(Evaluate(actual: "9.9") && Evaluate(actual: "10.2"), "Diameter boundaries are inclusive");
        Check(!Evaluate(actual: "9.89") && !Evaluate(actual: "10.21"), "Diameter outside either tolerance fails");
        Check(Evaluate(lower: "0.1"), "Unsigned lower tolerance matches the negative tolerance shown on the card");
        Check(!Evaluate(surface: "合格"), "Surface qualified does not meet the explicitly confirmed rule");
        Check(!Evaluate(strength: "500"), "Strength inside the range does not meet the confirmed rule");
        Check(!Evaluate(strength: "400") && !Evaluate(strength: "600"), "Strength boundaries are in range");
        Check(Evaluate(strength: "399") && Evaluate(strength: "601"), "Strength outside either end meets the condition");
        Check(!Evaluate(coil: "不合格"), "Unqualified coil diameter fails");
        Check(!Evaluate(pitch: "不合格"), "Unqualified coil pitch fails");
        foreach (var range in new[] { "400 ~ 600 MPa", "400～600", "400至600", "400—600", "400到600" })
            Check(Evaluate(range: range), "Explicit strength range separators are supported");
        foreach (var range in new string?[] { null, "", "未知", "600-400", "400-600-800" })
            Check(!Evaluate(range: range), "Missing or ambiguous strength range cannot qualify");
        Check(!Evaluate(actual: null) && !Evaluate(actual: "abc"), "Incomplete diameter input fails");
        Check(!Evaluate(lower: null) && !Evaluate(upper: null) && !Evaluate(diameter: null), "Missing diameter requirements fail");
        Check(!Evaluate(strength: null) && !Evaluate(strength: "-1"), "Missing or invalid strength fails");
        var page = new Page();
        page.UpdateAutoInspectionResult();
        Check(page.InspectResultPicker.SelectedIndex == 0, "All five conditions select qualified in the UI");
        page.InspectResultPicker.SelectedIndex = 1;
        Check(page.InspectResultPicker.SelectedIndex == 1, "User can override automatic qualified to unqualified");
        page.SurfaceJudgmentPicker.SelectedItem = "合格";
        page.UpdateAutoInspectionResult();
        Check(page.InspectResultPicker.SelectedIndex == 1, "Any failed condition selects unqualified in the UI");
        page.InspectResultPicker.SelectedIndex = 0;
        Check(page.InspectResultPicker.SelectedIndex == 0, "User can override automatic unqualified to qualified");
        page.UpdateAutoInspectionResult();
        Check(page.InspectResultPicker.SelectedIndex == 1, "Reevaluating changed inputs applies the rule again");
        page._detail!.Process = "酸洗"; page.InspectResultPicker.SelectedIndex = 0;
        page.UpdateAutoInspectionResult();
        Check(page.InspectResultPicker.SelectedIndex == 0, "Acid UI retains manual result");
        page._detail.Process = "拉拔"; page._inspectStatus = "3";
        page.UpdateAutoInspectionResult();
        Check(page.InspectResultPicker.SelectedIndex == 0, "Completed inspection result stays unchanged");
        Console.WriteLine("PASS: " + checks + " production quality automatic result assertions");
    }
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
}
'@
Add-Type -TypeDefinition ("#nullable enable`nusing System;`n" + $rule + $ui + $tests)
[QualityAutoTests.Tests]::Run()
