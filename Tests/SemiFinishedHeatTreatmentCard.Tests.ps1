$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$modelSource = Get-Content (Join-Path $root 'Models/Quality/IncomingQualityOrderDto.cs') -Raw
$models = "namespace SemiFinishedCardTests;`n" +
    [regex]::Match($modelSource, '(?s)public sealed class ProductionQualityScanMaterialDto\b.*?(?=public sealed class)').Value +
    $modelSource.Substring($modelSource.IndexOf('public sealed class ProductionQualityDetailDto'))
$page = Get-Content (Join-Path $root 'Pages/Quality/MachineQualityDetailPage.xaml.cs') -Raw
$methods = foreach ($name in @('IsSemiFinishedHeatTreatment', 'BuildInfoRows', 'ApplyScannedProductionCardFields',
    'ResolveCurrentFlowName', 'ResolveMachine', 'ResolveProductDiameter', 'ResolvePieceNo',
    'ResolveProcessCardDate', 'ResolveHeatTreatmentCardDate', 'FormatSignedTolerance', 'FormatCoilPitchControl')) {
    $method = [regex]::Match($page, "(?s)    private [^\r\n]+\b$name\(.*?(?=\r?\n    private )").Value
    if (!$method) { throw "Method not found: $name" }
    $method
}
$support = @'
public class Page
{
    private string? _listInspectionSchemeName = null, _processNameFromScan = null;
    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
    private static bool IsHeatTreatmentScheme(string? name) => name?.Contains("热处理") == true;
    private static bool IsPicklingScheme(string? name) => name?.Contains("酸洗") == true;
    private static bool IsProcessCardScheme(string? name) => name?.Contains("拉拔") == true || name?.Contains("开坯") == true;
'@
$cases = @'
    public static void Run()
    {
        var p = new Page();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var material = JsonSerializer.Deserialize<ProductionQualityScanMaterialDto>("""
        {"productionType":"半成品","billetInputDiameterMm":"12.5","billetUpperTolerance":0.15,
         "billetLowerTolerance":"-0.2","coilDiameter":"480-680","plateNo":"PLATE-01",
         "machineNo":"M-01","customerCode":"C-01","furnaceNo":"F-01","steelGrade":"S-01",
         "listing":"挂牌A","inputSpecification":"旧挂牌","originPlace":"产地A","inputDiameterMm":"8.5",
         "productDiameter":"6.5","coilDiameterControl":"收紧","coilPitchControl":"80","pieceNo":"WRONG"}
        """, options)!;
        var detail = new ProductionQualityDetailDto { processName = "热处理", inspectDate = "2026-11-12", workOrderNo = "W-01", standardDiameterMm = "9.5" };
        p.ApplyScannedProductionCardFields(detail, material);
        Check(p.IsSemiFinishedHeatTreatment(detail), "Semi-finished heat treatment selects the production card");
        var rows = p.BuildInfoRows(detail);
        string[] labels = { "日期", "机台", "客户代码", "炉号", "钢号", "挂牌", "工号", "盘号", "产地", "投料直径", "开胚投料直径", "成品直径", "上公差", "下公差", "圈径", "圈径控制", "圈距控制" };
        string[] values = { "20261112", "M-01", "C-01", "F-01", "S-01", "挂牌A", "W-01", "PLATE-01", "产地A", "8.5", "12.5", "6.5", "+0.15", "-0.2", "480-680", "收紧", "≤80mm" };
        Check(rows.Length == 17, "Card contains exactly 17 fields");
        for (int i = 0; i < labels.Length; i++)
            Check(rows[i].Label == labels[i] && rows[i].Value == values[i], "Correct label, order and API source for " + labels[i]);
        Check(detail.standardDiameterMm == "9.5", "Card diameters do not overwrite the inspection standard diameter");

        var missing = JsonSerializer.Deserialize<ProductionQualityScanMaterialDto>("""
        {"productionType":"半成品","billetUpperTolerance":null,"plateNo":null,"pieceNo":"NO-FALLBACK"}
        """, options)!;
        p.ApplyScannedProductionCardFields(detail, missing);
        rows = p.BuildInfoRows(detail);
        foreach (var row in rows.Skip(1).Where(row => row.Label != "工号"))
            Check(string.IsNullOrEmpty(row.Value), "Missing values clear the previous material's " + row.Label);
        Check(detail.billetUpperTolerance == null && detail.billetLowerTolerance == null, "Null and omitted decimal tolerances stay nullable");

        missing.productionType = "成品";
        p.ApplyScannedProductionCardFields(detail, missing);
        Check(!p.IsSemiFinishedHeatTreatment(detail) && p.BuildInfoRows(detail).Length == 9, "Finished heat treatment retains the original card");
        missing.productionType = null;
        p.ApplyScannedProductionCardFields(detail, missing);
        Check(!p.IsSemiFinishedHeatTreatment(detail), "Missing production type clears the previous semi-finished flag");
        detail.productionType = "半成品";
        detail.processName = "拉拔";
        Check(!p.IsSemiFinishedHeatTreatment(detail) && p.BuildInfoRows(detail).Length == 17, "Semi-finished drawing retains its original process card");
        detail.processName = "酸洗";
        Check(p.BuildInfoRows(detail).Length == 1, "Pickling retains its original card");
        detail.processName = null;
        detail.inspectionSchemeName = "热处理抽检";
        detail.productionType = " 半成品 ";
        Check(p.IsSemiFinishedHeatTreatment(detail), "Scheme fallback and trimmed production type select heat treatment");
        detail.prodcutDiameter = "7.5";
        Check(p.BuildInfoRows(detail)[11].Value == "7.5", "Legacy product diameter remains supported");
        var refreshed = JsonSerializer.Deserialize<ProductionQualityDetailDto>("""
        {"processName":"热处理","productionType":"半成品","plateNo":"REFRESHED","billetInputDiameterMm":"13","billetUpperTolerance":null}
        """, options)!;
        Check(p.IsSemiFinishedHeatTreatment(refreshed) && p.BuildInfoRows(refreshed)[7].Value == "REFRESHED" &&
            p.BuildInfoRows(refreshed)[10].Value == "13", "Detail refresh can deserialize and display the new fields");
        Console.WriteLine("PASS: " + checks + " semi-finished heat treatment card assertions");
    }
    private static int checks;
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
}
'@
Add-Type -TypeDefinition ("#nullable enable`nusing System; using System.Linq; using System.Collections.Generic; using System.Globalization; using System.Text.Json;`n" + $models + $support + ($methods -join "`n") + $cases) -WarningAction SilentlyContinue
[SemiFinishedCardTests.Page]::Run()
