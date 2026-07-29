using System.Text.Json;
using System.Text.Json.Serialization;

namespace JXHLJSApp.Models;

/// <summary>
/// 员工二维码中与扫码登录有关的字段。
/// 二维码可以包含更多字段，未声明字段会被 System.Text.Json 自动忽略。
/// </summary>
public sealed class QrLoginPayload
{
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

        var json = rawValue?.Trim();
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
}
