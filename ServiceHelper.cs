using System;
using Microsoft.Extensions.DependencyInjection;

namespace JXHLJSApp;

/// <summary>
/// 全局依赖注入助手，跨平台安全访问 Application.Services。
/// </summary>
public static class ServiceHelper
{
    public static IServiceProvider Services =>
#if ANDROID
        Microsoft.Maui.MauiApplication.Current.Services;
#else
        Microsoft.Maui.Controls.Application.Current.Services;
#endif

    public static T GetService<T>() where T : notnull =>
        Services.GetRequiredService<T>();
}
