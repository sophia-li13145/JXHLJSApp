namespace JXHLJSApp.Tools;

internal static class TaskListVisibility
{
    public const string ConfigCode = "pda_task_list_jurisdiction";

    public static bool CanShow(string? configValue, string? username)
    {
        var value = configValue?.Trim();
        if (string.IsNullOrEmpty(value)) return false;
        if (string.Equals(value, "all", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.IsNullOrWhiteSpace(username)) return false;

        return value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Contains(username.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
