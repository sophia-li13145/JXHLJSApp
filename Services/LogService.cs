namespace JXHLJSApp.Services;

public sealed class LogService
{
    private const string LogPattern = "app_log-*.txt";

    public string LogsDirectory => Path.Combine(FileSystem.Current.AppDataDirectory, "logs");

    public string? GetLatestLogFile()
    {
        if (!Directory.Exists(LogsDirectory))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(LogsDirectory, LogPattern)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    public string ReadLatestLog(int maxLength = 20000)
    {
        var latest = GetLatestLogFile();
        if (latest is null)
        {
            return Directory.Exists(LogsDirectory) ? "暂无日志文件。" : "暂无日志目录。";
        }

        var text = File.ReadAllText(latest);
        return text.Length > maxLength ? text[^maxLength..] : text;
    }
}
