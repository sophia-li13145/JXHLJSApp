$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$productionSource = (Get-Content -LiteralPath (Join-Path $repoRoot 'Tools/HardwareScanProtocol.cs') -Raw) + "`n" +
    (Get-Content -LiteralPath (Join-Path $repoRoot 'Tools/HardwareScanTextBuffer.cs') -Raw)
$productionSource = $productionSource.Replace('namespace JXHLJSApp.Tools;', '')
$cases = @'
public static class Regression
{
    private static int _assertions;
    public static void Run()
    {
        Check(HardwareScanProtocol.GetActions("lc").Contains("nlscan.action.SCANNER_RESULT"), "Newland broadcast must be listened to without changing lc configuration");
        Check(HardwareScanProtocol.GetActions("custom.action").SequenceEqual(new[] { "custom.action", "lc", "nlscan.action.SCANNER_RESULT" }), "Custom and legacy broadcast profiles remain available");
        Check(HardwareScanProtocol.GetActions("lc").Count() == 2, "Duplicate actions are removed");
        Check(HardwareScanProtocol.GetActions(null).Count() == 2, "Missing custom configuration retains device defaults");

        var extras = new Dictionary<string, string?> { ["SCAN_BARCODE1"] = "工单-001", ["SCAN_STATE"] = "ok" };
        string? Read(string key) => extras.TryGetValue(key, out var value) ? value : null;
        Check(HardwareScanProtocol.ReadResult(Read, "data") == "工单-001", "Newland primary data field must be accepted");
        extras["SCAN_STATE"] = "fail";
        Check(HardwareScanProtocol.ReadResult(Read, "data") == null, "Failed Newland decode must not submit a stale barcode");
        extras["SCAN_STATE"] = "OK";
        extras["SCAN_BARCODE1"] = " ";
        extras["SCAN_BARCODE2"] = "second";
        Check(HardwareScanProtocol.ReadResult(Read, "data") == "second", "Second Newland result is a fallback for an empty primary");
        extras.Clear();
        extras["data"] = "legacy";
        Check(HardwareScanProtocol.ReadResult(Read, "data") == "legacy", "lc/data scans must continue working");
        extras["custom_key"] = "custom";
        Check(HardwareScanProtocol.ReadResult(Read, "custom_key") == "custom", "Configured field has priority");
        extras.Clear();
        extras["com.symbol.datawedge.data_string"] = "zebra";
        Check(HardwareScanProtocol.ReadResult(Read, "data") == "zebra", "Existing alternative fields remain supported");
        extras.Clear();
        Check(HardwareScanProtocol.ReadResult(Read, "data") == null, "An empty broadcast must not invoke business logic");

        var buffer = new HardwareScanTextBuffer();
        buffer.Update("direct-fill-no-enter", 1, 0);
        Check(!buffer.TryTake(1, 749, out _), "Direct fill waits for a complete result");
        Check(buffer.TryTake(1, 750, out var code) && code == "direct-fill-no-enter", "Default Newland text output must complete without Enter");
        Check(!buffer.TryTake(1, 1000, out _), "One injection is consumed exactly once");
        buffer.Update("A", 2, 1000);
        Check(!buffer.TryTake(2, 1200, out _), "A slow PDA must not submit the initial character at 150 ms");
        buffer.Update("AB", 2, 1300);
        Check(!buffer.TryTake(2, 1750, out _), "A character burst must not submit the partial barcode");
        Check(buffer.TryTake(2, 2050, out code) && code == "AB", "A character burst is coalesced into one barcode");
        buffer.Update("with-enter\r\n", 3, 2000);
        Check(buffer.TryTake(3, 2000, out code) && code == "with-enter\r\n", "An Enter suffix supports immediate completion");
        buffer.Update("{\n\"code\":\"A\"\n}", 3, 3000);
        Check(!buffer.TryTake(3, 3000, out _), "An internal JSON newline must not terminate scanning");
        Check(buffer.TryTake(3, 3750, out code) && code == "{\n\"code\":\"A\"\n}", "Multiline payload is preserved");
        buffer.Update("old-page", 4, 4000);
        Check(!buffer.TryTake(5, 5000, out _), "Input must not cross a navigation generation");
        Check(!buffer.TryTake(4, 5000, out _), "Discarded input cannot reappear on the old page");
        buffer.Update("cancelled", 5, 5000);
        buffer.Reset();
        Check(!buffer.TryTake(5, 6000, out _), "Background/focus reset must discard pending injection");
        buffer.Update(" \r\n", 6, 6000);
        Check(!buffer.TryTake(6, 7000, out _), "Empty injection is ignored");
        Console.WriteLine("PASS: " + _assertions + " hardware scan protocol/input assertions");
    }

    private static void Check(bool condition, string message)
    { _assertions++; if (!condition) throw new Exception(message); }
}
'@
$testSource = "#nullable enable`nusing System;`nusing System.Collections.Generic;`nusing System.Linq;`nnamespace JXHLJSApp.HardwareScanProtocolTests {`n" +
    $productionSource + $cases + "`n}"
Add-Type -TypeDefinition $testSource
[JXHLJSApp.HardwareScanProtocolTests.Regression]::Run()
