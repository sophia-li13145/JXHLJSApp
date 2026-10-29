using System.Diagnostics;
using AndroidX.Camera.Core;
using AndroidX.Lifecycle;
using JXHLJSApp.Tools;
using Serilog;
using ZXing.Net.Maui;

namespace JXHLJSApp.Platforms.Android;

public sealed partial class FocusedCameraBarcodeReaderHandler
{
    private const string ScannerRevision = "pda-device-rotation-20261028";
    private readonly ScannerRecoveryPolicy _recoveryPolicy = new();
    private ILifecycleOwner? _lifecycleOwner;
    private CameraStateObserver? _cameraStateObserver;
    private CancellationTokenSource? _cameraMonitor;
    private FrameMetrics _frameMetrics = new();
    private bool _recoveryLimitReported;
    private string _cameraStateName = "unknown";

    private bool IsCameraForeground() => _lifecycleOwner?.Lifecycle.CurrentState
        .IsAtLeast(global::AndroidX.Lifecycle.Lifecycle.State.Resumed!) == true
        && PlatformView.IsShown && PlatformView.IsAttachedToWindow;

    private void ResetFrameHeartbeat(FrameMetrics? metrics = null)
    {
        metrics ??= _frameMetrics;
        var now = Stopwatch.GetTimestamp();
        Volatile.Write(ref metrics.LastArrival, now);
        Volatile.Write(ref metrics.LastCompletion, now);
    }

    private void StartCameraDiagnostics(ILifecycleOwner owner, int generation, FrameMetrics metrics)
    {
        ResetFrameHeartbeat(metrics);
        Log.Information("Scanner session started: Revision={Revision}, Generation={Generation}, RecoveryAttempt={RecoveryAttempt}, Assembly={AssemblyId}",
            ScannerRevision, generation, _recoveryPolicy.Attempts, typeof(FocusedCameraBarcodeReaderHandler).Module.ModuleVersionId);
        _cameraStateObserver = new CameraStateObserver(state =>
        {
            if (generation != _generation) return;
            _cameraStateName = state.GetType().ToString() ?? "unknown";
            Log.Information("Scanner camera state: State={State}, Generation={Generation}", _cameraStateName, generation);
            if (state.Error is { } error)
                Log.Warning("Scanner camera error: Code={Code}, Type={Type}, Cause={Cause}, Generation={Generation}",
                    error.Code, error.Type, error.Cause, generation);
        });
        _camera!.CameraInfo.CameraState.Observe(owner, _cameraStateObserver);
        _cameraMonitor = new CancellationTokenSource();
        _ = MonitorCameraAsync(generation, metrics, _cameraMonitor.Token);
    }

    private async Task MonitorCameraAsync(int generation, FrameMetrics metrics, CancellationToken ct)
    {
        var wasForeground = IsCameraForeground();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (ct.IsCancellationRequested || generation != _generation || !_connected || !VirtualView.IsDetecting) return;
                    var foreground = IsCameraForeground();
                    if (!foreground)
                    {
                        wasForeground = false;
                        ResetFrameHeartbeat(metrics);
                        return;
                    }
                    if (SynchronizeCameraRotations()) ScheduleRotationFocus(generation);
                    if (!wasForeground)
                    {
                        wasForeground = true;
                        ResetFrameHeartbeat(metrics);
                        Interlocked.Exchange(ref metrics.ConsecutiveErrors, 0);
                        ScheduleRotationFocus(generation);
                    }
                    var now = Stopwatch.GetTimestamp();
                    var frameAge = Stopwatch.GetElapsedTime(Volatile.Read(ref metrics.LastArrival), now);
                    var processingAge = Stopwatch.GetElapsedTime(Volatile.Read(ref metrics.LastCompletion), now);
                    Log.Information("Scanner analysis status: Revision={Revision}, Generation={Generation}, Frames={Frames}, Processed={Processed}, Errors={Errors}, Streaming={Streaming}, Camera={CameraState}, PreviewRotation={PreviewRotation}, AnalysisRotation={AnalysisRotation}, FocusPending={FocusPending}, FrameAgeMs={FrameAgeMs}, CompletionAgeMs={CompletionAgeMs}",
                        ScannerRevision, generation, Volatile.Read(ref metrics.Received), Volatile.Read(ref metrics.Processed),
                        Volatile.Read(ref metrics.Errors), _streaming, _cameraStateName, _preview?.TargetRotation,
                        _analysis?.TargetRotation, _focusFuture is not null, frameAge.TotalMilliseconds, processingAge.TotalMilliseconds);
                    RecoverCamera(generation, frameAge, processingAge, Volatile.Read(ref metrics.ConsecutiveErrors));
                });
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { Log.Warning(ex, "Scanner camera monitor failed"); }
    }

    private void RecoverCamera(int generation, TimeSpan frameAge, TimeSpan completionAge, int consecutiveErrors)
    {
        if (generation != _generation || !_connected || !VirtualView.IsDetecting) return;
        var decision = _recoveryPolicy.Evaluate(IsCameraForeground(), _streaming, frameAge, completionAge, consecutiveErrors);
        if (decision == ScannerRecoveryDecision.None) return;
        if (decision == ScannerRecoveryDecision.LimitReached)
        {
            if (!_recoveryLimitReported)
            {
                _recoveryLimitReported = true;
                Log.Warning("Scanner automatic recovery exhausted: Generation={Generation}", generation);
                VirtualView.ReportCameraStatus("摄像头分析异常，请返回重试或使用扫码枪输入。");
            }
            return;
        }
        Log.Warning("Scanner analysis stalled; rebinding own use cases: Generation={Generation}, Attempt={Attempt}", generation, _recoveryPolicy.Attempts);
        StopCamera(resetRecoveryBudget: false);
        UpdateCamera();
    }

    private static bool RecordFrameArrival(FrameMetrics metrics)
    {
        Volatile.Write(ref metrics.LastArrival, Stopwatch.GetTimestamp());
        return Interlocked.Increment(ref metrics.Received) == 1;
    }

    private void RecordFrameFailure(Exception exception, int generation, string layout, FrameMetrics metrics)
    {
        if (generation != Volatile.Read(ref _generation)) return;
        Interlocked.Increment(ref metrics.Errors);
        Interlocked.Increment(ref metrics.ConsecutiveErrors);
        var now = Stopwatch.GetTimestamp();
        var last = Volatile.Read(ref metrics.LastErrorLog);
        if (last != 0 && Stopwatch.GetElapsedTime(last, now) < TimeSpan.FromSeconds(5)) return;
        Interlocked.Exchange(ref metrics.LastErrorLog, now);
        Log.Warning(exception, "Scanner frame analysis failed: Revision={Revision}, Generation={Generation}, Layout={Layout}",
            ScannerRevision, generation, layout);
    }

    private static string DescribeFrame(IImageProxy image)
    {
        try
        {
            var plane = image.GetPlanes()[0];
            var buffer = plane.Buffer;
            return $"format={image.Format}; size={image.Width}x{image.Height}; crop={image.CropRect}; rotation={image.ImageInfo.RotationDegrees}; rowStride={plane.RowStride}; pixelStride={plane.PixelStride}; position={buffer.Position()}; limit={buffer.Limit()}; remaining={buffer.Remaining()}";
        }
        catch (Exception ex) { return $"metadata unavailable: {ex.GetType().Name}"; }
    }

    private void StopCameraDiagnostics()
    {
        _cameraMonitor?.Cancel();
        _cameraMonitor?.Dispose();
        _cameraMonitor = null;
        if (_cameraStateObserver is not null)
        {
            _camera?.CameraInfo.CameraState.RemoveObserver(_cameraStateObserver);
            _cameraStateObserver.Dispose();
            _cameraStateObserver = null;
        }
    }

    private sealed class CameraStateObserver(Action<CameraState> changed) : Java.Lang.Object, IObserver
    {
        public void OnChanged(Java.Lang.Object? value)
        {
            if (value is CameraState state) changed(state);
        }
    }

    // Each generation owns its decoder, lock and counters. Recovery never waits on an old decoder's lock.
    private sealed class FrameMetrics
    {
        public readonly object DecoderLock = new();
        public CameraBarcodeDecoder? Decoder;
        public BarcodeReaderOptions? DecoderOptions;
        public long LastArrival = Stopwatch.GetTimestamp();
        public long LastCompletion = Stopwatch.GetTimestamp();
        public long LastErrorLog;
        public int Received;
        public int Processed;
        public int Errors;
        public int ConsecutiveErrors;
        public int DetailsPending = 1;
    }
}
