using Android.Content;
using Android.Views;
using Google.Common.Util.Concurrent;
using JXHLJSApp.Tools;
using Serilog;

namespace JXHLJSApp.Platforms.Android;

public sealed partial class FocusedCameraBarcodeReaderHandler
{
    private DeviceOrientationListener? _orientationListener;
    private ScannerOrientationTracker? _orientationTracker;
    private EventHandler<global::Android.Views.View.LayoutChangeEventArgs>? _layoutChanged;
    private CancellationTokenSource? _rotationSettling;

    private int GetDisplayRotation() => (int)(PlatformView.Display?.Rotation ?? SurfaceOrientation.Rotation0);

    private void StartOrientationTracking(int generation)
    {
        _orientationListener = new DeviceOrientationListener(Context, orientation =>
        {
            if (!_connected || generation != _generation || !VirtualView.IsDetecting || _orientationTracker is null) return;
            if (!_orientationTracker.TryUpdate(orientation, out var rotation)) return;
            Log.Information("Scanner device rotated: Revision={Revision}, Generation={Generation}, Orientation={Orientation}, AnalysisRotation={Rotation}",
                ScannerRevision, generation, orientation, rotation);
            SynchronizeCameraRotations();
            ScheduleRotationFocus(generation);
        });
        if (_orientationListener.CanDetectOrientation()) _orientationListener.Enable();
        else
        {
            _orientationListener.Dispose();
            _orientationListener = null;
            Log.Information("Scanner orientation sensor unavailable; using display rotation");
        }
        _layoutChanged = (_, _) =>
        {
            if (generation != _generation || !_connected) return;
            if (SynchronizeCameraRotations()) ScheduleRotationFocus(generation);
        };
        PlatformView.LayoutChange += _layoutChanged;
    }

    private bool SynchronizeCameraRotations()
    {
        if (_preview is null || _analysis is null) return false;
        try
        {
            // A locked UI must keep its preview aligned with the display, even upside down.
            // Analysis follows physical orientation, including reverse portrait without a UI rotation.
            var displayRotation = GetDisplayRotation();
            var analysisRotation = _orientationListener is null
                ? displayRotation : _orientationTracker?.SurfaceRotation ?? displayRotation;
            var changed = _preview.TargetRotation != displayRotation || _analysis.TargetRotation != analysisRotation;
            if (!changed) return false;
            _preview.TargetRotation = displayRotation;
            _analysis.TargetRotation = analysisRotation;
            Volatile.Write(ref _frameMetrics.DetailsPending, 1);
            Log.Information("Scanner rotations synchronized: Preview={PreviewRotation}, Analysis={AnalysisRotation}, Generation={Generation}",
                displayRotation, analysisRotation, _generation);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Scanner rotation synchronization failed");
            return false;
        }
    }

    private void ScheduleRotationFocus(int generation)
    {
        _rotationSettling?.Cancel();
        _rotationSettling?.Dispose();
        CancelFocus();
        _rotationSettling = new CancellationTokenSource();
        _ = FocusAfterRotationAsync(generation, _rotationSettling.Token);
    }

    private async Task FocusAfterRotationAsync(int generation, CancellationToken ct)
    {
        try
        {
            await Task.Delay(350, ct);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (ct.IsCancellationRequested || generation != _generation || !_connected) return;
                _rotationSettling?.Dispose();
                _rotationSettling = null;
                // Display rotation can be delivered after the physical sensor event.
                SynchronizeCameraRotations();
                ResetFrameHeartbeat();
                RestartFocusCycle();
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private void StopOrientationTracking()
    {
        _rotationSettling?.Cancel();
        _rotationSettling?.Dispose();
        _rotationSettling = null;
        if (_layoutChanged is not null) PlatformView.LayoutChange -= _layoutChanged;
        _layoutChanged = null;
        _orientationListener?.Disable();
        _orientationListener?.Dispose();
        _orientationListener = null;
        _orientationTracker = null;
    }

    private async Task EnforceFocusTimeoutAsync(IListenableFuture future, int generation, CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (ct.IsCancellationRequested || generation != _generation || !ReferenceEquals(_focusFuture, future)) return;
                Log.Warning("Scanner focus timed out: Revision={Revision}, Generation={Generation}", ScannerRevision, generation);
                CancelFocus();
                if (_connected && VirtualView.IsDetecting)
                    VirtualView.ReportCameraStatus("对焦超时，请调整距离或点击画面重试。");
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private void CancelFocusTimeout()
    {
        _focusTimeout?.Cancel();
        _focusTimeout?.Dispose();
        _focusTimeout = null;
    }

    private sealed class DeviceOrientationListener(Context context, Action<int> changed) : OrientationEventListener(context)
    {
        public override void OnOrientationChanged(int orientation) => changed(orientation);
    }
}
