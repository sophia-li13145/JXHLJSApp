// Utils/TokenStorage.cs
namespace JXHLJSApp;

public static class TokenStorage
{
    private const string Key = "auth_token";

    public static async Task SaveAsync(string token)
    {
        var normalized = NormalizeToken(token);

        // 先写 Preferences 兜底缓存，避免部分安卓设备 SecureStorage 异常时 token 完全未保存。
        Preferences.Set(Key, normalized);

        try
        {
            await SecureStorage.SetAsync(Key, normalized);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TokenStorage] SecureStorage save error: {ex.Message}");
        }

        System.Diagnostics.Debug.WriteLine($"[TokenStorage] Saved token len={normalized?.Length}");
    }

    public static async Task<string?> LoadAsync()
    {
        try
        {
            var t = await SecureStorage.GetAsync(Key);
            if (!string.IsNullOrWhiteSpace(t))
            {
                var normalized = NormalizeToken(t);
                System.Diagnostics.Debug.WriteLine($"[TokenStorage] Loaded from SecureStorage, len={normalized?.Length}");
                return normalized;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TokenStorage] SecureStorage error: {ex.Message}");
        }

        // 兜底
        var fallback = NormalizeToken(Preferences.Get(Key, null));
        System.Diagnostics.Debug.WriteLine($"[TokenStorage] Loaded from Preferences, len={fallback?.Length}");
        return fallback;
    }

    public static string NormalizeToken(string? token)
    {
        var value = token?.Trim() ?? string.Empty;
        const string bearerPrefix = "Bearer ";
        if (value.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            value = value[bearerPrefix.Length..].Trim();
        }

        return value;
    }

    public static Task ClearAsync()
    {
        try { SecureStorage.Remove(Key); } catch { }
        Preferences.Remove(Key);
        ApiClient.SetBearer(null);
        System.Diagnostics.Debug.WriteLine("[TokenStorage] Cleared");
        return Task.CompletedTask;
    }
}
