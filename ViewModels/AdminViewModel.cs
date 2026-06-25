using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text.Json.Nodes;

namespace JXHLJSApp.ViewModels;

public sealed class AdminViewModel : ObservableObject
{
    private readonly IConfigLoader _configLoader;
    private string _scheme = "http";
    private string _host = string.Empty;
    private string _serviceName = "normalService";
    private string _servicePath = "/normalService";
    private string _message = string.Empty;
    private Color _messageColor = Color.FromArgb("#1E7E34");
    private bool _isMessageVisible;

    public AdminViewModel(IConfigLoader configLoader)
    {
        _configLoader = configLoader;
        ReloadCommand = new RelayCommand(LoadConfig);
        SaveCommand = new RelayCommand(SaveConfig);
        LoadConfig();
    }

    public IRelayCommand ReloadCommand { get; }
    public IRelayCommand SaveCommand { get; }

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
        IsMessageVisible = false;
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
