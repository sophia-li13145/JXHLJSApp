using JXHLJSApp.Models;
using JXHLJSApp.Services;
using Serilog;
using System.Text.Json;
using ZXing.Net.Maui;

namespace JXHLJSApp.Pages;

public partial class LoginPage : ContentPage
{
    private readonly IAuthApi _authApi;
    private readonly IScanService _scanService;
    private bool _isBusy;
    private bool _credentialsLoaded;
    private bool _isPasswordVisible;
    private bool _isQrLoginTab;

    public LoginPage(IAuthApi authApi, IScanService scanService)
    {
        InitializeComponent();
        _authApi = authApi;
        _scanService = scanService;
        VersionLabel.Text = $"V{AppInfo.Current.VersionString}";

        // 默认展示账号登录。
        SetLoginTab(isQrLogin: false);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadRememberedCredentialsAsync();

        if (_isQrLoginTab)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(UsernameEntry.Text))
        {
            UsernameEntry.Focus();
        }
        else if (string.IsNullOrWhiteSpace(PasswordEntry.Text))
        {
            PasswordEntry.Focus();
        }
    }

    private async void OnPasswordCompleted(object sender, EventArgs e)
    {
        await LoginAsync();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        await LoginAsync();
    }

    private void OnAccountLoginTabTapped(object sender, TappedEventArgs e)
    {
        if (_isBusy) return;

        SetLoginTab(isQrLogin: false);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (string.IsNullOrWhiteSpace(UsernameEntry.Text))
            {
                UsernameEntry.Focus();
            }
            else
            {
                PasswordEntry.Focus();
            }
        });
    }

    private void OnQrLoginTabTapped(object sender, TappedEventArgs e)
    {
        if (_isBusy) return;
        SetLoginTab(isQrLogin: true);
    }

    private async void OnQrScanTapped(object sender, TappedEventArgs e)
    {
        await ScanQrAndLoginAsync();
    }

    private async void OnRememberCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if (!e.Value)
        {
            await RememberedLoginStore.ClearAsync();
        }
    }

    private void OnTogglePasswordVisibilityClicked(object sender, EventArgs e)
    {
        SetPasswordVisibility(!_isPasswordVisible);
    }

    private async void OnAdminTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(AppShell.RouteAdmin);
    }

    private async void OnLogTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(AppShell.RouteLog);
    }

    private async Task LoginAsync()
    {
        if (_isBusy)
        {
            return;
        }

        var username = UsernameEntry.Text?.Trim() ?? string.Empty;
        var password = PasswordEntry.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowMessage("请输入您的账号");
            UsernameEntry.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ShowMessage("请输入密码");
            PasswordEntry.Focus();
            return;
        }

        try
        {
            SetBusy(true);
            var result = await _authApi.LoginAsync(username, password);
            var loginSucceeded = await CompleteLoginAsync(
                result,
                fallbackUsername: username,
                fallbackWorkNumber: null,
                afterLoginAsync: () => SaveRememberedCredentialsAsync(username, password));

            if (!loginSucceeded)
            {
                return;
            }
        }
        catch (Exception ex)
        {
            ShowMessage($"登录失败：{ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ScanQrAndLoginAsync()
    {
        if (_isBusy)
        {
            return;
        }

        try
        {
            ClearMessage();
            SetBusy(true);
            QrStatusLabel.Text = "请将员工二维码对准扫码框";

            var rawValue = await _scanService.ScanAsync(
                "扫描员工二维码",
                formats: BarcodeFormat.QrCode);
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                QrStatusLabel.Text = "已取消扫码，请重新扫描";
                return;
            }

            if (!QrLoginPayload.TryParse(rawValue, out var qrPayload, out var parseError) || qrPayload is null)
            {
                var escapedRawValue = JsonSerializer.Serialize(rawValue);
                Log.Warning(
                    "员工二维码解析失败，Length={Length}，RawValue={RawValue}",
                    rawValue.Length,
                    escapedRawValue);

                QrStatusLabel.Text = "二维码识别失败";
                ShowMessage(parseError);
                return;
            }

            QrStatusLabel.Text = "二维码已识别，正在登录...";
            var result = await _authApi.QrLoginAsync(qrPayload.Username!, qrPayload.WorkNumber!);
            var loginSucceeded = await CompleteLoginAsync(
                result,
                fallbackUsername: qrPayload.Username!,
                fallbackWorkNumber: qrPayload.WorkNumber,
                afterLoginAsync: null);

            if (!loginSucceeded)
            {
                QrStatusLabel.Text = "扫码登录失败，请重新扫描";
            }
        }
        catch (Exception ex)
        {
            QrStatusLabel.Text = "扫码登录失败，请重新扫描";
            ShowMessage($"扫码登录失败：{ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>
    /// 账号密码登录和二维码登录共用的登录成功处理，保证后续逻辑完全一致。
    /// </summary>
    private async Task<bool> CompleteLoginAsync(
        LoginResult result,
        string fallbackUsername,
        string? fallbackWorkNumber,
        Func<Task>? afterLoginAsync)
    {
        if (!result.Success || string.IsNullOrWhiteSpace(result.Token))
        {
            ShowMessage(string.IsNullOrWhiteSpace(result.Message)
                ? "登录失败，请检查登录信息"
                : result.Message);
            return false;
        }

        if (result.UserInfo is not null)
        {
            if (string.IsNullOrWhiteSpace(result.UserInfo.username))
            {
                result.UserInfo.username = fallbackUsername;
            }

            if (string.IsNullOrWhiteSpace(result.UserInfo.workNumber)
                && !string.IsNullOrWhiteSpace(fallbackWorkNumber))
            {
                result.UserInfo.workNumber = fallbackWorkNumber;
            }
        }

        await TokenStorage.SaveAsync(result.Token);
        ApiClient.SetBearer(result.Token);
        UserSessionStore.Save(result.UserInfo);
        Preferences.Set(UserSessionKeys.UserName, fallbackUsername);

        if (afterLoginAsync is not null)
        {
            await afterLoginAsync();
        }

        ShowMessage("登录成功", isError: false);
        App.SwitchToLoggedInShell();
        return true;
    }

    private async Task LoadRememberedCredentialsAsync()
    {
        if (_credentialsLoaded)
        {
            return;
        }

        _credentialsLoaded = true;

        var credentials = await RememberedLoginStore.LoadAsync();
        RememberCheckBox.IsChecked = credentials.RememberPassword;

        if (!string.IsNullOrWhiteSpace(credentials.Username))
        {
            UsernameEntry.Text = credentials.Username;
        }

        if (credentials.RememberPassword && !string.IsNullOrEmpty(credentials.Password))
        {
            PasswordEntry.Text = credentials.Password;
            ShowMessage("已为您填充上次保存的账号和密码", isError: false);
        }
    }

    private async Task SaveRememberedCredentialsAsync(string username, string password)
    {
        if (RememberCheckBox.IsChecked)
        {
            await RememberedLoginStore.SaveAsync(username, password);
            return;
        }

        await RememberedLoginStore.ClearAsync();
        RememberedLoginStore.SaveUsername(username);
    }

    private void SetLoginTab(bool isQrLogin)
    {
        _isQrLoginTab = isQrLogin;
        AccountLoginPanel.IsVisible = !isQrLogin;
        QrLoginPanel.IsVisible = isQrLogin;

        AccountLoginTabBorder.BackgroundColor = isQrLogin ? Colors.Transparent : Colors.White;
        QrLoginTabBorder.BackgroundColor = isQrLogin ? Colors.White : Colors.Transparent;

        AccountLoginTabLabel.TextColor = Color.FromArgb(isQrLogin ? "#5D718D" : "#082D63");
        QrLoginTabLabel.TextColor = Color.FromArgb(isQrLogin ? "#082D63" : "#5D718D");
        AccountLoginTabLabel.FontAttributes = isQrLogin ? FontAttributes.None : FontAttributes.Bold;
        QrLoginTabLabel.FontAttributes = isQrLogin ? FontAttributes.Bold : FontAttributes.None;

        AccountLoginTabBorder.Shadow = isQrLogin
            ? null
            : new Shadow { Brush = new SolidColorBrush(Color.FromArgb("#22000000")), Offset = new Point(0, 2), Radius = 4 };
        QrLoginTabBorder.Shadow = isQrLogin
            ? new Shadow { Brush = new SolidColorBrush(Color.FromArgb("#22000000")), Offset = new Point(0, 2), Radius = 4 }
            : null;

        ClearMessage();
    }

    private void SetPasswordVisibility(bool isVisible)
    {
        _isPasswordVisible = isVisible;
        PasswordEntry.IsPassword = !isVisible;
        TogglePasswordVisibilityButton.Text = isVisible ? "隐藏" : "显示";
        SemanticProperties.SetDescription(TogglePasswordVisibilityButton, isVisible ? "隐藏密码" : "显示密码");
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;

        LoginButton.IsEnabled = !isBusy;
        LoginButton.Text = isBusy ? "登 录 中..." : "登 录 系 统";

        QrScanCard.InputTransparent = isBusy;
        QrLoginIndicator.IsVisible = isBusy && _isQrLoginTab;
        QrLoginIndicator.IsRunning = isBusy && _isQrLoginTab;

        AccountLoginTabBorder.InputTransparent = isBusy;
        QrLoginTabBorder.InputTransparent = isBusy;
    }

    private void ClearMessage()
    {
        MessageLabel.Text = string.Empty;
        MessageLabel.IsVisible = false;
    }

    private void ShowMessage(string message, bool isError = true)
    {
        MessageLabel.Text = message;
        MessageLabel.TextColor = isError ? Color.FromArgb("#C0392B") : Color.FromArgb("#1E7E34");
        MessageLabel.IsVisible = true;
    }
}
