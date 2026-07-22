using JXHLJSApp.Services;

namespace JXHLJSApp.Pages;

public partial class LoginPage : ContentPage
{
    private readonly IAuthApi _authApi;
    private bool _isBusy;
    private bool _credentialsLoaded;
    private bool _isPasswordVisible;

    public LoginPage(IAuthApi authApi)
    {
        InitializeComponent();
        _authApi = authApi;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadRememberedCredentialsAsync();

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

            if (!result.Success || string.IsNullOrWhiteSpace(result.Token))
            {
                ShowMessage(string.IsNullOrWhiteSpace(result.Message) ? "登录失败，请检查账号或密码" : result.Message);
                return;
            }

            if (result.UserInfo is not null && string.IsNullOrWhiteSpace(result.UserInfo.username))
            {
                result.UserInfo.username = username;
            }

            await TokenStorage.SaveAsync(result.Token);
            ApiClient.SetBearer(result.Token);
            UserSessionStore.Save(result.UserInfo);
            Preferences.Set(UserSessionKeys.UserName, username);
            await SaveRememberedCredentialsAsync(username, password);
            ShowMessage("登录成功", isError: false);
            App.SwitchToLoggedInShell();
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
    }

    private void ShowMessage(string message, bool isError = true)
    {
        MessageLabel.Text = message;
        MessageLabel.TextColor = isError ? Color.FromArgb("#C0392B") : Color.FromArgb("#1E7E34");
        MessageLabel.IsVisible = true;
    }
}
