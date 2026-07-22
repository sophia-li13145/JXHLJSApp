namespace JXHLJSApp;

public sealed record RememberedLoginCredentials(bool RememberPassword, string Username, string? Password);

public static class RememberedLoginStore
{
    private const string RememberPasswordKey = "RememberPassword";
    private const string UsernameKey = "RememberedUserName";
    private const string LegacyUsernameKey = "UserName";
    private const string PasswordKey = "Password";

    public static async Task<RememberedLoginCredentials> LoadAsync()
    {
        var rememberPassword = Preferences.Get(RememberPasswordKey, false);
        var username = Preferences.Get(UsernameKey, Preferences.Get(LegacyUsernameKey, string.Empty));

        if (!rememberPassword)
        {
            await RemovePasswordAsync();
            return new RememberedLoginCredentials(false, username, null);
        }

        var password = await LoadPasswordAsync();
        return new RememberedLoginCredentials(true, username, password);
    }

    public static async Task SaveAsync(string username, string password)
    {
        SaveUsername(username);
        Preferences.Set(RememberPasswordKey, true);

        try
        {
            await SecureStorage.SetAsync(PasswordKey, password);
            Preferences.Remove(PasswordKey);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RememberedLoginStore] SecureStorage save error: {ex.Message}");
            Preferences.Set(PasswordKey, password);
        }
    }

    public static void SaveUsername(string username)
    {
        Preferences.Set(UsernameKey, username);
    }

    public static async Task ClearAsync()
    {
        Preferences.Set(RememberPasswordKey, false);
        await RemovePasswordAsync();
    }

    private static async Task<string?> LoadPasswordAsync()
    {
        try
        {
            var password = await SecureStorage.GetAsync(PasswordKey);
            if (!string.IsNullOrEmpty(password))
            {
                Preferences.Remove(PasswordKey);
                return password;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RememberedLoginStore] SecureStorage load error: {ex.Message}");
        }

        var fallbackPassword = Preferences.Get(PasswordKey, null);
        if (string.IsNullOrEmpty(fallbackPassword))
        {
            return null;
        }

        try
        {
            await SecureStorage.SetAsync(PasswordKey, fallbackPassword);
            Preferences.Remove(PasswordKey);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RememberedLoginStore] SecureStorage migration error: {ex.Message}");
        }

        return fallbackPassword;
    }

    private static Task RemovePasswordAsync()
    {
        try
        {
            SecureStorage.Remove(PasswordKey);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RememberedLoginStore] SecureStorage remove error: {ex.Message}");
        }

        Preferences.Remove(PasswordKey);
        return Task.CompletedTask;
    }
}
