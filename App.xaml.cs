using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Serilog;
using System.IO;

namespace IndustrialControlMAUI;

public partial class App : Application
{
    private readonly IConfigLoader _configLoader;
    private readonly AppShell _shell;

    public static IServiceProvider? Services { get; set; }

    public App(IConfigLoader configLoader, AppShell shell)
    {
        InitializeComponent();

        _configLoader = configLoader;
        _shell = shell;

        // ===== 初始化 Serilog（跨平台安全路径）=====
        InitSerilog();

        // ===== 全局异常捕获 =====
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // 先给 MainPage，避免空引用
        MainPage = _shell;

        // 异步初始化登录状态
        _ = InitAsync();
    }

    /// <summary>
    /// Serilog 初始化（AppDataDirectory/logs）
    /// </summary>
    private static void InitSerilog()
    {
        var baseDir = FileSystem.Current.AppDataDirectory;

        // 极端兜底（理论上不会发生，但防御性处理）
        if (string.IsNullOrWhiteSpace(baseDir) || baseDir == "/")
        {
            baseDir = FileSystem.Current.CacheDirectory;
        }

        var logsDir = Path.Combine(baseDir, "logs");
        Directory.CreateDirectory(logsDir);

        var logPath = Path.Combine(logsDir, "app_log-.txt");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Debug()
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                shared: true)
            .CreateLogger();

        Log.Information("Serilog initialized. LogPath = {LogPath}", logPath);
    }

    protected override async void OnStart()
    {
        base.OnStart();

        // 启动时确保配置最新
        await _configLoader.EnsureLatestAsync();

        var token = await TokenStorage.LoadAsync();
        var isLoggedIn = !string.IsNullOrWhiteSpace(token);

        Log.Information("App started. IsLoggedIn = {IsLoggedIn}", isLoggedIn);
    }

    private async Task InitAsync()
    {
        var token = await TokenStorage.LoadAsync();
        bool authed = !string.IsNullOrWhiteSpace(token);

        _shell.ApplyAuth(authed);
    }

    public static void SwitchToLoggedInShell()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var sp = Current?.Handler?.MauiContext?.Services;
            var shell = sp?.GetRequiredService<AppShell>();
            if (shell == null) return;

            Current!.MainPage = shell;
            shell.ApplyAuth(true);
        });
    }

    public static void SwitchToLoggedOutShell()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var sp = Current?.Handler?.MauiContext?.Services;
            var shell = sp?.GetRequiredService<AppShell>();
            if (shell == null) return;

            Current!.MainPage = shell;
            shell.ApplyAuth(false);
        });
    }

    // ===== 全局异常处理 =====
    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log.Error(ex, "捕获到未处理的全局异常");
        }
    }

    private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "捕获到未观察的任务异常");
        e.SetObserved();
    }
}
