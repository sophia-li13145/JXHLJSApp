using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JXHLJSApp.Services;

namespace JXHLJSApp.ViewModels;

public sealed class LogsViewModel : ObservableObject
{
    private readonly LogService _logService;
    private string _logPath = string.Empty;
    private string _logContent = string.Empty;

    public LogsViewModel(LogService logService)
    {
        _logService = logService;
        RefreshCommand = new RelayCommand(Refresh);
        Refresh();
    }

    public IRelayCommand RefreshCommand { get; }

    public string LogPath
    {
        get => _logPath;
        set => SetProperty(ref _logPath, value);
    }

    public string LogContent
    {
        get => _logContent;
        set => SetProperty(ref _logContent, value);
    }

    public void Refresh()
    {
        LogPath = _logService.GetLatestLogFile() ?? _logService.LogsDirectory;
        LogContent = _logService.ReadLatestLog();
    }
}
