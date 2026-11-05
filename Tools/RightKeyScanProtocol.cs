namespace JXHLJSApp.Tools;

/// <summary>Explicit broadcast contract for the device scan service.</summary>
internal static class RightKeyScanProtocol
{
    public const string DefaultAction = "nlscan.action.SCANNER_RESULT";
    public const string DefaultDataKey = "SCAN_BARCODE1";

    public static IEnumerable<string> GetActions(string? configuredAction, string? legacyAction = null) =>
        new[] { configuredAction, legacyAction, DefaultAction, "lc" }
            .Where(action => !string.IsNullOrWhiteSpace(action))
            .Select(action => action!.Trim())
            .Distinct(StringComparer.Ordinal);

    public static string? ReadResult(Func<string, string?> read, string dataKey)
    {
        var state = read("SCAN_STATE");
        if (!string.IsNullOrWhiteSpace(state) && !string.Equals(state, "ok", StringComparison.OrdinalIgnoreCase))
            return null;
        foreach (var key in new[] { dataKey, DefaultDataKey, "SCAN_BARCODE2", "data" }.Distinct(StringComparer.Ordinal))
        {
            var code = read(key);
            if (!string.IsNullOrWhiteSpace(code)) return code;
        }
        return null;
    }
}
