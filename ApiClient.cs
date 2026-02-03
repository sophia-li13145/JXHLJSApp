using System.Net.Http.Headers;

namespace JXHLJSApp;

public static class ApiClient
{
    public static readonly HttpClient Instance = new HttpClient();

    /// <summary>
    /// 配置 HttpClient 的 BaseAddress。
    /// 传入完整的 baseUrl，例如: http://allysysindustrialsoft.aax6.cn:9128/normalService
    /// </summary>
    public static void ConfigureBase(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("BaseUrl 不能为空", nameof(baseUrl));

        // ✅ 确保末尾带斜杠，表示目录
        if (!baseUrl.EndsWith("/"))
            baseUrl += "/";

        Instance.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    }


    /// <summary>
    /// 设置 Bearer Token 认证头。
    /// </summary>
    public static void SetBearer(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            Instance.DefaultRequestHeaders.Authorization = null;
        }
        else
        {
            Instance.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}