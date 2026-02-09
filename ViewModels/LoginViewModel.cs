using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JXHLJSApp.Tools;
using System.Net.Http.Json;
using System.Text.Json;

namespace JXHLJSApp.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IConfigLoader _cfg;

    [ObservableProperty] private string userName = string.Empty;
    [ObservableProperty] private string password = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool showPassword; // false=默认隐藏
    [ObservableProperty] private bool rememberPassword; // 新增：记住密码

    /// <summary>执行 new 逻辑。</summary>
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    /// <summary>执行 LoginViewModel 初始化逻辑。</summary>
    public LoginViewModel(IConfigLoader cfg)
    {
        _cfg = cfg;

        // 启动时加载保存的账号
        UserName = Preferences.Get("UserName", string.Empty);
        Password = Preferences.Get("Password", string.Empty);
        RememberPassword = Preferences.Get("RememberPassword", false);
    }

    /// <summary>执行 LoginAsync 逻辑。</summary>
    [RelayCommand]
    public async Task LoginAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            // 1) 确保有本地配置
            await _cfg.EnsureLatestAsync();

            // 2) 登录验证前：根据用户名里的 @xxx 写入当前服务并保存
            _cfg.SetCurrentServiceByUser(UserName);

            // 3) 用最新配置拼 URL（端口已在 ipAddress 中）
            var baseUrl = _cfg.GetBaseUrl();                // scheme://ip:port + /{servicePath}
            var loginRel = _cfg.GetApiPath("login", "/pda/auth/login");
            var fullUrl = new Uri(baseUrl + loginRel);

            // 3) 表单校验后执行登录
            if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password))
            {
                await Application.Current.MainPage.DisplayAlert("提示", "请输入用户名和密码", "确定");
                return;
            }

            var payload = new { username = UserName, password = Password };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            var resp = await ApiClient.Instance.PostAsJsonAsync(fullUrl, payload, cts.Token);
            var raw = await ResponseGuard.ReadAsStringSafeAsync(resp.Content, cts.Token);

            if (!resp.IsSuccessStatusCode)
            {
                await Application.Current.MainPage.DisplayAlert("登录失败", $"HTTP {(int)resp.StatusCode}: {raw}", "确定");
                return;
            }

            var result = JsonSerializer.Deserialize<ApiResponse<LoginResult>>(raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var ok = (result?.success == true) || (result?.code is 0 or 200);
            var token = result?.result?.token;

            if (!ok || string.IsNullOrWhiteSpace(token))
            {
                await Application.Current.MainPage.DisplayAlert("登录失败", result?.message ?? "登录返回无效", "确定");
                return;
            }

            await TokenStorage.SaveAsync(token!);
            ApiClient.SetBearer(token);
            Preferences.Set("UserName", UserName ?? "");
            if (RememberPassword)
            {
                Preferences.Set("Password", Password ?? "");
                Preferences.Set("RememberPassword", true);
            }
            else
            {
                Preferences.Remove("Password");
                Preferences.Set("RememberPassword", false);
            }

            await CacheUserWorkshopInfoAsync(baseUrl, cts.Token);

            App.SwitchToLoggedInShell();
        }
        catch (OperationCanceledException)
        {
            await Application.Current.MainPage.DisplayAlert("超时", "登录请求超时，请检查网络", "确定");
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("异常", ex.Message, "确定");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>执行 TogglePassword 逻辑。</summary>
    [RelayCommand]
    private void TogglePassword() => ShowPassword = !ShowPassword;

    /// <summary>执行 ClearHistory 逻辑。</summary>
    [RelayCommand]
    private void ClearHistory()
    {
        UserName = string.Empty;
        Password = string.Empty;
        RememberPassword = false;

        Preferences.Remove("UserName");
        Preferences.Remove("Password");
        Preferences.Set("RememberPassword", false);
    }


    private sealed class ApiResponse<T>
    {
        public int code { get; set; }
        public bool success { get; set; }
        public string? message { get; set; }
        public T? result { get; set; }
    }

    private async Task CacheUserWorkshopInfoAsync(string baseUrl, CancellationToken ct)
    {
        var userInfoRel = _cfg.GetApiPath("auth.userinfo", "/pda/auth/getUserInfo");
        var fullUrl = new Uri(baseUrl + userInfoRel);

        using var req = new HttpRequestMessage(HttpMethod.Get, fullUrl);

        // getUserInfo 接口在部分环境下只认 token/satoken 头，不能只依赖 Authorization
        var token = await TokenStorage.LoadAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            req.Headers.Remove("token");
            req.Headers.Remove("satoken");
            req.Headers.Remove("Authorization");
            req.Headers.TryAddWithoutValidation("token", token);
            req.Headers.TryAddWithoutValidation("satoken", token);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        using var resp = await ApiClient.Instance.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            return;
        }

        var raw = await ResponseGuard.ReadAsStringSafeAsync(resp.Content, ct);
        var result = JsonSerializer.Deserialize<ApiResponse<UserInfoResult>>(raw, _json);
        var info = result?.result;
        if (info is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(info.workshopName))
            Preferences.Set("WorkshopName", info.workshopName);
        else
            Preferences.Remove("WorkshopName");

        if (!string.IsNullOrWhiteSpace(info.workshopScope))
            Preferences.Set("WorkshopScope", info.workshopScope);
        else
            Preferences.Remove("WorkshopScope");
    }

    private sealed class LoginResult
    {
        public string? token { get; set; }
        public object? userInfo { get; set; }
    }

    private sealed class UserInfoResult
    {
        public string? workshopName { get; set; }
        public string? workshopScope { get; set; }
    }
}
