namespace JXHLJSApp.Tools;

internal static class HardwareScanProtocol
{
    public const string NewlandAction = "nlscan.action.SCANNER_RESULT";

    public static IEnumerable<string> GetActions(string? configuredAction) =>
        new[] { configuredAction, "lc", NewlandAction }
            .Where(action => !string.IsNullOrWhiteSpace(action))
            .Select(action => action!.Trim())
            .Distinct(StringComparer.Ordinal);

    public static string? ReadResult(Func<string, string?> read, string configuredKey)
    {
        var state = read("SCAN_STATE");
        if (!string.IsNullOrWhiteSpace(state) && !string.Equals(state, "ok", StringComparison.OrdinalIgnoreCase))
            return null;

        foreach (var key in new[]
        {
            configuredKey, "SCAN_BARCODE1", "SCAN_BARCODE2", "data",
            "com.symbol.datawedge.data_string", "barcode_string", "scan_data",
            "barocode", "scan_result", "value", "barcode_data", "dataString"
        }.Distinct(StringComparer.Ordinal))
        {
            var value = read(key);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return null;
    }
}
