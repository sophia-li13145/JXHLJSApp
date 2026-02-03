// CrashTrap.cs  —— 不使用 Microsoft.Maui.Controls.Internals
using System;
using System.Reflection;
using System.Threading.Tasks;
#if ANDROID
using Android.Runtime;
#endif
namespace JXHLJSApp;
public static class CrashTrap
{
    public static void Init()
    {
        // 1) 首次机会异常（包含 XamlParse、绑定、Json、HTTP 等）
        AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
        {
            var ex = Unwrap(e.Exception);
            // 常见导致“没有兼容代码”的真实类型
            if (ex is Microsoft.Maui.Controls.Xaml.XamlParseException
             || ex is NullReferenceException
             || ex is InvalidCastException
             || ex is ArgumentException
             || ex is System.Text.Json.JsonException
             || ex is System.Net.Http.HttpRequestException)
            {
                System.Diagnostics.Debug.WriteLine($"[FirstChance] {ex.GetType().Name}: {ex.Message}\n{ex}");
                System.Diagnostics.Debugger.Break();
            }
        };

        // 2) 未观察到的 Task 异常
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            var ex = Unwrap(e.Exception);
            System.Diagnostics.Debug.WriteLine($"[UnobservedTask] {ex.GetType().Name}: {ex.Message}\n{ex}");
            e.SetObserved();
        };

        // 3) 进程级未处理异常
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = Unwrap(e.ExceptionObject as Exception ?? new Exception("Unknown fatal"));
            System.Diagnostics.Debug.WriteLine($"[Unhandled] {ex.GetType().Name}: {ex.Message}\n{ex}");
        };

#if ANDROID
        // 4) Java/原生侧异常（VS 黑框来自这里）
        AndroidEnvironment.UnhandledExceptionRaiser += (_, e) =>
        {
            var ex = e.Exception;
            System.Diagnostics.Debug.WriteLine($"[Java] {ex?.GetType()?.FullName}: {ex?.Message}\n{ex}");
            // 如需让程序继续跑，可设：e.Handled = true;
        };
#endif
    }

    private static Exception Unwrap(Exception ex)
    {
        while (true)
        {
            if (ex is TargetInvocationException tie && tie.InnerException != null) { ex = tie.InnerException; continue; }
            if (ex is AggregateException ae && ae.InnerException != null) { ex = ae.InnerException; continue; }
            return ex;
        }
    }
}
