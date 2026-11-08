$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$models = Get-Content (Join-Path $root 'Models/Quality/IncomingQualityOrderDto.cs') -Raw
$model = [regex]::Match($models, '(?s)public sealed class ProductionQualityOrderDto\s*\{.*?(?=\r?\npublic sealed class ProductionQualityDetailDto)').Value
if (!$model) { throw 'Production quality list DTO not found' }
$tests = @'
public static class Tests
{
    public static void Run()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        using var payload = JsonDocument.Parse("{\"result\":[{\"qualityNo\":\"Q1\",\"outputTime\":\"2026-11-08 17:42:08\"},{\"qualityNo\":\"Q2\",\"outputTime\":null},{\"qualityNo\":\"Q3\"}]}");
        var rows = payload.RootElement.GetProperty("result").Deserialize<List<ProductionQualityOrderDto>>(options)!;
        Check(rows.Count == 3 && rows[0].qualityNo == "Q1", "Existing list fields still deserialize with outputTime");
        Check(rows[0].outputTimeDisplay == "2026-11-08 17:42:08", "Display retains year, date, hours, minutes and seconds");
        Check(rows[1].outputTimeDisplay == "暂无记录" && rows[2].outputTimeDisplay == "暂无记录", "Null and omitted timestamps have a clear empty state");
        var item = new ProductionQualityOrderDto { outputTime = "2025-12-31T23:59:59+08:00" };
        Check(item.outputTimeDisplay == "2025-12-31 23:59:59", "ISO offset timestamp preserves its recorded wall-clock date and seconds");
        item.outputTime = "2026-11-08T09:02:03.123Z";
        Check(item.outputTimeDisplay == "2026-11-08 09:02:03", "ISO timestamps format consistently to seconds");
        item.outputTime = "   ";
        Check(item.outputTimeDisplay == "暂无记录", "Blank timestamps do not render an empty line");
        item.outputTime = " backend value ";
        Check(item.outputTimeDisplay == "backend value", "Unrecognized timestamps preserve backend information instead of hiding it");
        Console.WriteLine("PASS: " + checks + " production quality output time assertions");
    }
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
}
'@
Add-Type -TypeDefinition ("#nullable enable`nusing System; using System.Collections.Generic; using System.Linq; using System.Globalization; using System.Text.Json; namespace OutputTimeTests;`n" + $model + $tests)
[OutputTimeTests.Tests]::Run()
