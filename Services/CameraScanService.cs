using JXHLJSApp.Pages;

namespace JXHLJSApp.Services;

public sealed class CameraScanService
{
    public const string EnableCameraParameter = "enableCamera";

    private bool _isScanning;

    public async Task<string?> ScanAsync(Page owner, string title)
    {
        if (_isScanning) return null;
        _isScanning = true;
        try
        {
            if (Platform.AppContext.PackageManager?.HasSystemFeature("android.hardware.camera") != true)
            {
                await owner.DisplayAlert("无法扫码", "此设备没有可用的摄像头。", "确定");
                return null;
            }

            if (await Permissions.RequestAsync<Permissions.Camera>() != PermissionStatus.Granted)
            {
                await owner.DisplayAlert("需要相机权限", "请在系统设置中允许此应用使用相机。", "确定");
                return null;
            }

            if (Shell.Current?.CurrentPage != owner) return null;

            var page = new CameraScanPage(title);
            await owner.Navigation.PushModalAsync(page);
            var result = await page.Result;
            return Shell.Current?.CurrentPage == owner ? result : null;
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(owner, "无法打开摄像头", ex.Message);
            return null;
        }
        finally
        {
            _isScanning = false;
        }
    }
}
