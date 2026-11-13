$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$models = (Get-Content (Join-Path $root 'Models/Quality/IncomingQualityOrderDto.cs') -Raw).Replace('namespace JXHLJSApp.Models.Quality;', 'namespace HeatQuantityTests;')
$models = "namespace HeatQuantityTests;`n" + $models.Substring($models.IndexOf('public sealed class ProductionQualityDetailDto'))
$page = Get-Content (Join-Path $root 'Pages/Quality/MachineQualityDetailPage.xaml.cs') -Raw
$fill = [regex]::Match($page, '(?s)    private void FillHeatTreatmentInputs\(.*?(?=\r?\n    private )').Value
if (!$fill) { throw 'Heat treatment field rendering method not found' }
$harness = @'
public class Field { public string? Text; }
public class Page
{
    public Field TotalInspectQtyEntry = new(), InspectedQtyEntry = new(), UninspectedQtyEntry = new();
    public Field HeatActualDiameterEntry = new(), StandardDiameterEntry = new(), BrokenDiameterEntry = new();
    public Field SectionShrinkageLabel = new(), TensileStrengthEntry = new(), HeatElongationEntry = new(), TorsionEntry = new();
    private string CalculateSectionShrinkageText() => "-";
    private string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
'@
$cases = @'
    public static void Run()
    {
        var page = new Page();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        void Load(string json)
        {
            using var doc = JsonDocument.Parse(json);
            page.FillHeatTreatmentInputs(doc.RootElement.GetProperty("result").Deserialize<ProductionQualityDetailDto>(options)!);
        }
        Load("{\"result\":{\"totalInspectQty\":12.5,\"inspectedQty\":0,\"uninspectedQty\":12.5}}");
        Check(page.TotalInspectQtyEntry.Text == 12.5m.ToString(CultureInfo.CurrentCulture), "Total quantity renders the API decimal value");
        Check(page.InspectedQtyEntry.Text == "0", "Zero inspected quantity remains visible");
        Check(page.UninspectedQtyEntry.Text == 12.5m.ToString(CultureInfo.CurrentCulture), "Uninspected quantity renders its own API value");
        Load("{\"result\":{\"totalInspectQty\":\"20\",\"inspectedQty\":\"8\",\"uninspectedQty\":\"12\"}}");
        Check(page.TotalInspectQtyEntry.Text == "20" && page.InspectedQtyEntry.Text == "8" && page.UninspectedQtyEntry.Text == "12", "Refresh replaces quantities and accepts numeric strings like the API client");
        Load("{\"result\":{\"totalInspectQty\":null,\"inspectedQty\":null}}");
        Check(page.TotalInspectQtyEntry.Text == "—" && page.InspectedQtyEntry.Text == "—" && page.UninspectedQtyEntry.Text == "—", "Null and omitted quantities clear stale values without inventing zeroes");
        Load("{\"result\":{\"standardDiameterMm\":\"6.5\",\"productDiameter\":\"10\"}}");
        Check(page.StandardDiameterEntry.Text == "6.5", "Standard diameter uses standardDiameterMm even when product diameter differs");
        Load("{\"result\":{\"productDiameter\":\"10\"}}");
        Check(page.StandardDiameterEntry.Text == null, "Missing standard diameter clears the field without falling back to product diameter");
        Console.WriteLine("PASS: 7 heat treatment quantity and standard diameter assertions");
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
}
'@
Add-Type -TypeDefinition ("#nullable enable`nusing System; using System.Collections.Generic; using System.Linq; using System.Globalization; using System.Text.Json;`n" + $models + $harness + $fill + $cases)
[HeatQuantityTests.Page]::Run()
