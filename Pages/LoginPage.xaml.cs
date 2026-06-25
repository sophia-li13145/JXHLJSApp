using JXHLJSApp.Services;

namespace JXHLJSApp.Pages;

public partial class LoginPage : ContentPage
{
    private readonly IAuthApi _authApi;
    private bool _isBusy;

    public LoginPage(IAuthApi authApi)
    {
        InitializeComponent();
        _authApi = authApi;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UsernameEntry.Focus();
    }

    private async void OnPasswordCompleted(object sender, EventArgs e)
    {
        await LoginAsync();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        await LoginAsync();
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

            await TokenStorage.SaveAsync(result.Token);
            UserSessionStore.Save(result.UserInfo);
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
