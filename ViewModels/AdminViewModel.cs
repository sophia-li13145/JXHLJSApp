using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JXHLJSApp.Services;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace JXHLJSApp.ViewModels;

public sealed class AdminViewModel : ObservableObject
{
    private readonly IConfigLoader _configLoader;
    private readonly IAppVersionService _appVersionService;
    private string _scheme = "http";
    private string _host = string.Empty;
    private string _serviceName = "normalService";
    private string _servicePath = "/normalService";
    private string _printerHost = string.Empty;
    private string _printerName = string.Empty;
    private string _configVersion = "0";
    private string _message = string.Empty;
    private Color _messageColor = Color.FromArgb("#1E7E34");
    private bool _isMessageVisible;

    public AdminViewModel(IConfigLoader configLoader, IAppVersionService appVersionService)
    {
        _configLoader = configLoader;
        _appVersionService = appVersionService;
        ReloadCommand = new RelayCommand(LoadConfig);
        SaveCommand = new RelayCommand(SaveConfig);
        CheckUpdateCommand = new AsyncRelayCommand(CheckUpdateAsync);
        LoadConfig();
    }

    public IRelayCommand ReloadCommand { get; }
    public IRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand CheckUpdateCommand { get; }

    /// <summary>当前安装包版本，只读展示。</summary>
    public string AppVersion => $"V{AppInfo.Current.VersionString}";

    /// <summary>当前生效配置的 schemaVersion，只读展示。</summary>
    public string ConfigVersion
    {
        get => _configVersion;
        private set => SetProperty(ref _configVersion, value);
    }

    public string Scheme
    {
        get => _scheme;
        set => SetProperty(ref _scheme, value);
    }

    public string Host
    {
        get => _host;
        set => SetProperty(ref _host, value);
    }

    public string ServiceName
    {
        get => _serviceName;
        set => SetProperty(ref _serviceName, value);
    }

    public string ServicePath
    {
        get => _servicePath;
        set => SetProperty(ref _servicePath, value);
    }

    public string PrinterHost
    {
        get => _printerHost;
        set => SetProperty(ref _printerHost, value);
    }

    public string PrinterName
    {
        get => _printerName;
        set => SetProperty(ref _printerName, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public Color MessageColor
    {
        get => _messageColor;
        set => SetProperty(ref _messageColor, value);
    }

    public bool IsMessageVisible
    {
        get => _isMessageVisible;
        set => SetProperty(ref _isMessageVisible, value);
    }

    private void LoadConfig()
    {
        var cfg = _configLoader.Load();
        var services = cfg?["services"] as JsonObject;
        var current = services?["current"]?.GetValue<string>() ?? "normalService";

        Scheme = cfg?["server"]?["scheme"]?.GetValue<string>() ?? "http";
        Host = cfg?["server"]?["ipAddress"]?.GetValue<string>() ?? string.Empty;
        ServiceName = current;
        ServicePath = services?[current]?.GetValue<string>()
            ?? services?["normalService"]?.GetValue<string>()
            ?? "/normalService";
        PrinterHost = cfg?["printing"]?["serverAddress"]?.GetValue<string>() ?? string.Empty;
        PrinterName = cfg?["printing"]?["printerName"]?.GetValue<string>() ?? string.Empty;
        ConfigVersion = ReadSchemaVersion(cfg);
        OnPropertyChanged(nameof(AppVersion));
        IsMessageVisible = false;
    }

    private async Task CheckUpdateAsync()
    {
        // 复用启动检查逻辑：无新版本或检查失败时由服务内部记录日志，这里不重复弹窗。
        await _appVersionService.CheckManuallyAsync();
        ShowMessage("版本检查已完成。", false);
    }

    /// <summary>读取 schemaVersion，兼容数字与字符串两种写法。</summary>
    private static string ReadSchemaVersion(JsonNode? cfg)
    {
        var value = cfg?["schemaVersion"];
        if (value is null)
        {
            return "0";
        }

        return value.GetValueKind() switch
        {
            JsonValueKind.Number => value.ToJsonString(),
            JsonValueKind.String => value.GetValue<string>()?.Trim() is { Length: > 0 } text ? text : "0",
            _ => "0"
        };
    }

    private void SaveConfig()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            ShowMessage("请输入服务器 IP / 域名。", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(ServiceName))
        {
            ShowMessage("请输入当前服务名。", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(PrinterHost))
        {
            ShowMessage("请输入打印服务 IP / 域名和端口。", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(PrinterName))
        {
            ShowMessage("请输入打印机名称。", true);
            return;
        }

        var cfg = _configLoader.Load() as JsonObject ?? new JsonObject();
        if (cfg["server"] is not JsonObject server)
        {
            server = new JsonObject();
            cfg["server"] = server;
        }

        server["scheme"] = string.Equals(Scheme, "https", StringComparison.OrdinalIgnoreCase) ? "https" : "http";
        server["ipAddress"] = Host.Trim();

        if (cfg["services"] is not JsonObject services)
        {
            services = new JsonObject();
            cfg["services"] = services;
        }

        var serviceName = ServiceName.Trim();
        services["current"] = serviceName;
        services[serviceName] = NormalizePath(ServicePath, "/normalService");

        if (cfg["printing"] is not JsonObject printing)
        {
            printing = new JsonObject();
            cfg["printing"] = printing;
        }

        printing["serverAddress"] = PrinterHost.Trim().TrimEnd('/');
        printing["printerName"] = PrinterName.Trim();
        printing["printJobsPath"] ??= "/api/v1/print-jobs";

        _configLoader.Save(cfg);
        ShowMessage("配置已保存。", false);
    }

    private void ShowMessage(string message, bool isError)
    {
        Message = message;
        MessageColor = isError ? Color.FromArgb("#C0392B") : Color.FromArgb("#1E7E34");
        IsMessageVisible = true;
    }

    private static string NormalizePath(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var trimmed = value.Trim();
        return trimmed.StartsWith('/') ? trimmed : $"/{trimmed}";
    }
}
