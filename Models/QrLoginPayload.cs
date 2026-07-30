using System.Text.Json;
using System.Text.Json.Serialization;

namespace JXHLJSApp.Models;

/// <summary>
/// 员工二维码中与扫码登录有关的字段。
/// 二维码可以包含更多字段，未声明字段会被 System.Text.Json 自动忽略。
/// </summary>
public sealed class QrLoginPayload
{
    private static readonly char[] ScannerPaddingCharacters =
    {
        '\uFEFF', // UTF-8 BOM decoded as a character by some scanner SDKs.
        '\u200B', // Zero-width space.
        '\u200C',
        '\u200D',
        '\u2060',
        '\0',
        '\u0002', // STX, commonly configured as an industrial scanner prefix.
        '\u0003', // ETX, commonly configured as an industrial scanner suffix.
        '\u001D'  // GS, used as a group separator by some scanners.
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("workNumber")]
    public string? WorkNumber { get; init; }

    public static bool TryParse(string? rawValue, out QrLoginPayload? payload, out string errorMessage)
    {
        payload = null;
        errorMessage = string.Empty;

        var json = NormalizeScannerValue(rawValue);
        if (string.IsNullOrWhiteSpace(json))
        {
            errorMessage = "二维码内容为空。";
            return false;
        }

        try
        {
            // 兼容二维码内容本身是被再次 JSON 编码的字符串，例如：
            // "{\"username\":\"admin\",\"workNumber\":\"07291\"}"
            if (json.Length >= 2 && json[0] == '"' && json[^1] == '"')
            {
                var unwrapped = JsonSerializer.Deserialize<string>(json, JsonOptions);
                if (!string.IsNullOrWhiteSpace(unwrapped))
                {
                    json = unwrapped.Trim();
                }
            }

            payload = JsonSerializer.Deserialize<QrLoginPayload>(json, JsonOptions);
        }
        catch (JsonException)
        {
            errorMessage = "二维码内容不是有效的员工 JSON。";
            return false;
        }

        if (payload is null)
        {
            errorMessage = "二维码中没有员工信息。";
            return false;
        }

        var username = payload.Username?.Trim();
        var workNumber = payload.WorkNumber?.Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(workNumber))
        {
            errorMessage = "二维码缺少 username 或 workNumber。";
            payload = null;
            return false;
        }

        payload = new QrLoginPayload
        {
            Username = username,
            WorkNumber = workNumber
        };
        return true;
    }

    private static string? NormalizeScannerValue(string? rawValue)
    {
        var value = rawValue?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        // Camera/scanner implementations may retain a BOM or other invisible
        // transport padding around the QR payload. JsonSerializer does not
        // regard these characters as JSON whitespace.
        return value.Trim(ScannerPaddingCharacters).Trim();
    }
}
