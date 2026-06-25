using CommunityToolkit.Maui;
using JXHLJSApp.Pages;
using JXHLJSApp.Services;
using JXHLJSApp.Tools;
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

            // Core framework and configuration services.
            builder.Services.AddSingleton<IConfigLoader, ConfigLoader>();
            builder.Services.AddSingleton<LogService>();
            builder.Services.AddSingleton<IDialogService, DialogService>();
            builder.Services.AddSingleton<AuthState>();
            builder.Services.AddTransient<AuthHeaderHandler>();
            builder.Services.AddTransient<TokenExpiredHandler>();
            builder.Services.AddSingleton<ScanService>();

            builder.Services.AddHttpClient<IWorkOrderApi, WorkOrderApi>(ConfigureBaseAddress)
                .AddHttpMessageHandler<AuthHeaderHandler>()
                .AddHttpMessageHandler<TokenExpiredHandler>();

            builder.Services.AddHttpClient<IInboundMaterialService, InboundMaterialService>(ConfigureBaseAddress)
                .AddHttpMessageHandler<AuthHeaderHandler>()
                .AddHttpMessageHandler<TokenExpiredHandler>();

            builder.Services.AddHttpClient<IIncomingStockService, IncomingStockService>(ConfigureBaseAddress)
                .AddHttpMessageHandler<AuthHeaderHandler>()
                .AddHttpMessageHandler<TokenExpiredHandler>();

            builder.Services.AddHttpClient<IOutboundMaterialService, OutboundMaterialService>(ConfigureBaseAddress)
                .AddHttpMessageHandler<AuthHeaderHandler>()
                .AddHttpMessageHandler<TokenExpiredHandler>();

            builder.Services.AddHttpClient<IMoldApi, MoldApi>(ConfigureBaseAddress)
                .AddHttpMessageHandler<AuthHeaderHandler>()
                .AddHttpMessageHandler<TokenExpiredHandler>();

            builder.Services.AddHttpClient<IWarehouseService, WarehouseService>(ConfigureBaseAddress)
                .AddHttpMessageHandler<AuthHeaderHandler>()
                .AddHttpMessageHandler<TokenExpiredHandler>();

            builder.Services.AddHttpClient<IQualityApi, QualityApi>(ConfigureBaseAddress)
                .AddHttpMessageHandler<AuthHeaderHandler>()
                .AddHttpMessageHandler<TokenExpiredHandler>();

            builder.Services.AddHttpClient<IAuthApi, AuthApi>(ConfigureBaseAddress)
                .AddHttpMessageHandler<AuthHeaderHandler>()
                .AddHttpMessageHandler<TokenExpiredHandler>();

            builder.Services.AddHttpClient<IEquipmentApi, EquipmentApi>(ConfigureBaseAddress)
                .AddHttpMessageHandler<AuthHeaderHandler>()
                .AddHttpMessageHandler<TokenExpiredHandler>();

            builder.Services.AddHttpClient<IAttachmentApi, AttachmentApi>(ConfigureBaseAddress)
                .AddHttpMessageHandler<AuthHeaderHandler>()
                .AddHttpMessageHandler<TokenExpiredHandler>();

            builder.Services.AddHttpClient<IEnergyApi, EnergyApi>(ConfigureBaseAddress)
                .AddHttpMessageHandler<AuthHeaderHandler>()
                .AddHttpMessageHandler<TokenExpiredHandler>();

            var app = builder.Build();
            App.Services = app.Services;
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
