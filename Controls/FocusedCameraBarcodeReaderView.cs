using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace JXHLJSApp.Controls;

// The Android handler supplies the focus commands missing in ZXing.Net.Maui 0.4.0.
public sealed class FocusedCameraBarcodeReaderView : CameraBarcodeReaderView, ICameraView
{
    public event EventHandler<string>? CameraStatusChanged;

    public void ReportCameraStatus(string message) => CameraStatusChanged?.Invoke(this, message);

    // The base control forwards through a StrongHandler of the library's concrete type.
    // Dispatch directly so our Android handler receives focus commands too.
    void ICameraView.AutoFocus() => Handler?.Invoke(nameof(ICameraView.AutoFocus));

    void ICameraView.Focus(Point point) => Handler?.Invoke(nameof(ICameraView.Focus), point);
}
