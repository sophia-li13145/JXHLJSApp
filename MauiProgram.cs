using CommunityToolkit.Maui;
using JXHLJSApp.Pages;
using JXHLJSApp.Services;
using JXHLJSApp.Services.WorkOrders;
using JXHLJSApp.Tools;
using JXHLJSApp.ViewModels;
using Microsoft.Extensions.Logging;
using Serilog;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;


namespace JXHLJSApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .UseBarcodeReader()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });
#if DEBUG
            builder.Logging.AddDebug(); // ✅ 放在 Build 之前
#endif
            // =====================================================
            // ✅ Serilog 初始化（统一安全路径）
            // =====================================================
            InitSerilog(builder);
            builder.Services.AddSingleton<AppShell>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<AdminPage>();
            builder.Services.AddTransient<LogPage>();
            builder.Services.AddTransient<RoleHomePage>();
            builder.Services.AddTransient<JXHLJSApp.Pages.WorkOrders.WorkOrderTaskListPage>();
            builder.Services.AddTransient<JXHLJSApp.Pages.WorkStart.WorkStartScanPage>();
            builder.Services.AddTransient<JXHLJSApp.Pages.WorkStart.WorkStartOrdersPage>();
            builder.Services.AddTransient<AdminViewModel>();
            builder.Services.AddTransient<LogsViewModel>();
            builder.Services.AddSingleton<LogService>();
            builder.Services.AddSingleton<IScanService, ScanService>();
            builder.Services.AddSingleton<AuthState>();
            builder.Services.AddTransient<AuthHeaderHandler>();
            builder.Services.AddTransient<TokenExpiredHandler>();

            // Core framework and configuration services.
            builder.Services.AddSingleton<IConfigLoader, ConfigLoader>();
            builder.Services.AddHttpClient<IAuthApi, AuthApi>(ConfigureBaseAddress)
                .AddHttpMessageHandler<AuthHeaderHandler>()
                .AddHttpMessageHandler<TokenExpiredHandler>();
            builder.Services.AddHttpClient<IWorkOrderApi, WorkOrderApi>(ConfigureBaseAddress)
                .AddHttpMessageHandler<AuthHeaderHandler>()
                .AddHttpMessageHandler<TokenExpiredHandler>();

            var app = builder.Build();
            //CrashTrap.Init(); //Debug
            return app;
        }

        // =====================================================
        // Serilog 初始化方法
        // =====================================================
        private static void InitSerilog(MauiAppBuilder builder)
        {
            var baseDir = FileSystem.Current.AppDataDirectory;

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

            builder.Logging.AddSerilog(Log.Logger, dispose: true);
        }

        private static void ConfigureBaseAddress(IServiceProvider sp, HttpClient http)
        {
            var cfg = sp.GetRequiredService<IConfigLoader>();
            var baseUrl = cfg.GetBaseUrl();

            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new InvalidOperationException("配置文件缺少有效的 BaseUrl。");

            if (baseUrl.EndsWith("/"))
                baseUrl = baseUrl.TrimEnd('/');

            http.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
        }
    }
}
