using System.Diagnostics;
using Android.Hardware.Camera2;
using AndroidX.Camera.Camera2.InterOp;
using AndroidX.Camera.Core;
using AndroidX.Camera.Lifecycle;
using AndroidX.Camera.View;
using AndroidX.Core.Content;
using AndroidX.Lifecycle;
using Google.Common.Util.Concurrent;
using Java.Util.Concurrent;
using JXHLJSApp.Controls;
using JXHLJSApp.Tools;
using Microsoft.Maui.Handlers;
using Serilog;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Readers;
using Point = Microsoft.Maui.Graphics.Point;
using Size = Microsoft.Maui.Graphics.Size;

namespace JXHLJSApp.Platforms.Android;

public sealed partial class FocusedCameraBarcodeReaderHandler : ViewHandler<FocusedCameraBarcodeReaderView, PreviewView>
{
    private static readonly IPropertyMapper<FocusedCameraBarcodeReaderView, FocusedCameraBarcodeReaderHandler> Mapper =
        new PropertyMapper<FocusedCameraBarcodeReaderView, FocusedCameraBarcodeReaderHandler>(ViewMapper)
        {
            [nameof(ICameraBarcodeReaderView.IsDetecting)] = (handler, _) => handler.UpdateCamera(),
            [nameof(IView.IsEnabled)] = (handler, view) =>
            {
                MapIsEnabled(handler, view);
                handler.UpdateCamera();
            },
            [nameof(ICameraView.CameraLocation)] = (handler, _) => { handler.StopCamera(); handler.UpdateCamera(); },
            [nameof(ICameraView.IsTorchOn)] = (handler, view) => handler._camera?.CameraControl.EnableTorch(view.IsTorchOn)
        };

    private static readonly CommandMapper<FocusedCameraBarcodeReaderView, FocusedCameraBarcodeReaderHandler> Commands =
        new(ViewCommandMapper)
        {
            [nameof(ICameraView.AutoFocus)] = (handler, _, _) => handler.FocusAt(null),
            [nameof(ICameraView.Focus)] = (handler, _, point) => handler.FocusAt(point is Point position ? position : null)
        };

    private readonly Stopwatch _sessionWatch = new();
    private readonly Stopwatch _lastFocus = new();
    private ProcessCameraProvider? _provider;
    private Preview? _preview;
    private ImageAnalysis? _analysis;
    private ICamera? _camera;
    private IExecutorService? _executor;
    private PreviewObserver? _previewObserver;
    private FrameAnalyzer? _analyzer;
    private IListenableFuture? _focusFuture;
    private CancellationTokenSource? _focusRetries;
    private CancellationTokenSource? _focusTimeout;
    private bool _connected;
    private bool _starting;
    private bool _streaming;
    private bool _supportsFocus = true;
    private int _generation;

    public FocusedCameraBarcodeReaderHandler() : base(Mapper, Commands) { }

    protected override PreviewView CreatePlatformView()
    {
        var preview = new PreviewView(Context);
        // COMPATIBLE makes STREAMING an accurate preview-ready signal and supports overlays.
        preview.SetImplementationMode(PreviewView.ImplementationMode.Compatible!);
        return preview;
    }

    protected override void ConnectHandler(PreviewView platformView)
    {
        base.ConnectHandler(platformView);
        _connected = true;
        UpdateCamera();
    }

    protected override void DisconnectHandler(PreviewView platformView)
    {
        _connected = false;
        StopCamera();
        base.DisconnectHandler(platformView);
    }

    private void UpdateCamera()
    {
        if (!_connected) return;
        if (!VirtualView.IsDetecting || !VirtualView.IsEnabled)
        {
            StopCamera();
            return;
        }
        if (_starting || _camera is not null) return;

        _starting = true;
        var generation = _generation;
        var view = VirtualView;
        var metrics = new FrameMetrics();
        _frameMetrics = metrics;
        view.ReportCameraStatus("正在启动摄像头…");
        _sessionWatch.Restart();
        var future = ProcessCameraProvider.GetInstance(Context);
        future.AddListener(new Java.Lang.Runnable(() =>
        {
            try
            {
                if (!_connected || generation != _generation || !view.IsDetecting) return;
                _provider = (ProcessCameraProvider)future.Get()!;
                var owner = Context as ILifecycleOwner ?? Platform.CurrentActivity as ILifecycleOwner
                    ?? throw new InvalidOperationException("无法获取摄像头生命周期。");
                _lifecycleOwner = owner;
                var displayRotation = GetDisplayRotation();
                _orientationTracker = new ScannerOrientationTracker(displayRotation);
                var selector = view.CameraLocation == CameraLocation.Front
                    ? CameraSelector.DefaultFrontCamera : CameraSelector.DefaultBackCamera;
                _executor = Executors.NewSingleThreadExecutor()
                    ?? throw new InvalidOperationException("无法启动摄像头分析线程。");
                _preview = new Preview.Builder().Build();
                _preview.TargetRotation = displayRotation;
                _preview.SetSurfaceProvider(PlatformView.SurfaceProvider);
                _analysis = new ImageAnalysis.Builder()
                    .SetDefaultResolution(new global::Android.Util.Size(640, 480))
                    .SetBackpressureStrategy(ImageAnalysis.StrategyKeepOnlyLatest)
                    .Build();
                _analysis.TargetRotation = displayRotation;
                _analyzer = new FrameAnalyzer(image => Analyze(image, view, generation, metrics));
                _analysis.SetAnalyzer(_executor, _analyzer);
                _camera = _provider.BindToLifecycle(owner, selector, _preview, _analysis);
                StartCameraDiagnostics(owner, generation, metrics);
                StartOrientationTracking(generation);
                _supportsFocus = SupportsAutoFocus(_camera);
                _camera.CameraControl.EnableTorch(view.IsTorchOn);
                _previewObserver = new PreviewObserver(streaming => OnPreviewStateChanged(streaming, generation));
                PlatformView.PreviewStreamState.Observe(owner, _previewObserver);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Scanner camera initialization failed");
                StopCamera();
                view.ReportCameraStatus("摄像头启动失败，可返回重试或使用扫码枪输入。");
            }
            finally
            {
                if (generation == _generation) _starting = false;
                future.Dispose();
            }
        }), ContextCompat.GetMainExecutor(Context));
    }

    private static bool SupportsAutoFocus(ICamera camera)
    {
        try
        {
            var characteristics = Camera2CameraInfo.ExtractCameraCharacteristics(camera.CameraInfo);
            var minimumDistance = characteristics.Get(CameraCharacteristics.LensInfoMinimumFocusDistance) as Java.Lang.Float;
            return minimumDistance is null || minimumDistance.FloatValue() > 0;
        }
        catch (Exception ex)
        {
            // A vendor's incomplete characteristics must not prevent camera decoding.
            Log.Debug(ex, "Scanner focus capability query unavailable");
            return true;
        }
    }

    private void OnPreviewStateChanged(bool streaming, int generation)
    {
        if (generation != _generation || !_connected || !VirtualView.IsDetecting) return;
        if (!streaming)
        {
            _streaming = false;
            CancelFocus();
            return;
        }
        if (_streaming) return;
        _streaming = true;
        ResetFrameHeartbeat();
        VirtualView.ReportCameraStatus("请对准二维码，点击画面可重新对焦。");
        if (_rotationSettling is null) RestartFocusCycle();
    }

    private void RestartFocusCycle()
    {
        if (!_connected || !_streaming || !VirtualView.IsDetecting || !IsCameraForeground()) return;
        var generation = _generation;
        CancelFocus();
        FocusAt(null);
        if (_supportsFocus)
        {
            _focusRetries = new CancellationTokenSource();
            _ = RetryFocusAsync(generation, _focusRetries.Token);
        }
    }

    private async Task RetryFocusAsync(int generation, CancellationToken ct)
    {
        try
        {
            // Two bounded retries, spaced beyond the two-second focus lock.
            for (var attempt = 0; attempt < 2; attempt++)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (ct.IsCancellationRequested || generation != _generation) return;
                    if (!_lastFocus.IsRunning || _lastFocus.Elapsed >= TimeSpan.FromSeconds(3)) FocusAt(null);
                });
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private void FocusAt(Point? position)
    {
        if (!_connected || !_streaming || !VirtualView.IsDetecting || !IsCameraForeground() || _camera is null ||
            PlatformView.Width <= 0 || PlatformView.Height <= 0 || _focusFuture is not null) return;
        if (!_supportsFocus)
        {
            VirtualView.ReportCameraStatus("当前摄像头为固定焦距，请调整二维码距离。");
            return;
        }

        var generation = _generation;
        var view = VirtualView;
        var watch = Stopwatch.StartNew();
        try
        {
            // MAUI focus coordinates are DIPs; the factory expects native preview pixels.
            var density = Context.Resources?.DisplayMetrics?.Density ?? 1;
            var x = position.HasValue ? (float)position.Value.X * density : PlatformView.Width / 2f;
            var y = position.HasValue ? (float)position.Value.Y * density : PlatformView.Height / 2f;
            var point = PlatformView.MeteringPointFactory.CreatePoint(
                Math.Clamp(x, 0, PlatformView.Width - 1), Math.Clamp(y, 0, PlatformView.Height - 1));
            using var action = new FocusMeteringAction.Builder(point, FocusMeteringAction.FlagAf | FocusMeteringAction.FlagAe)
                .SetAutoCancelDuration(2, TimeUnit.Seconds!).Build();
            var future = _camera.CameraControl.StartFocusAndMetering(action);
            _focusFuture = future;
            _focusTimeout = new CancellationTokenSource();
            _ = EnforceFocusTimeoutAsync(future, generation, _focusTimeout.Token);
            _lastFocus.Restart();
            view.ReportCameraStatus("正在对焦…");
            future.AddListener(new Java.Lang.Runnable(() =>
            {
                try
                {
                    if (generation != _generation || !ReferenceEquals(_focusFuture, future)) return;
                    var result = future.Get() as FocusMeteringResult;
                    Log.Information("Scanner focus completed: Success={Success}, ElapsedMs={ElapsedMs}",
                        result?.IsFocusSuccessful, watch.ElapsedMilliseconds);
                    view.ReportCameraStatus(result?.IsFocusSuccessful == true
                        ? "已对焦，请对准二维码；点击画面可重新对焦。"
                        : "未能合焦，请调整距离或点击画面重新对焦。");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Scanner focus unavailable: Revision={Revision}, Generation={Generation}", ScannerRevision, generation);
                    if (generation == _generation) view.ReportCameraStatus("请调整距离，点击画面可重新对焦。");
                }
                finally
                {
                    if (ReferenceEquals(_focusFuture, future))
                    {
                        _focusFuture = null;
                        CancelFocusTimeout();
                    }
                    future.Dispose();
                }
            }), ContextCompat.GetMainExecutor(Context));
        }
        catch (Exception ex)
        {
            CancelFocus();
            Log.Warning(ex, "Scanner focus request failed");
            view.ReportCameraStatus("请调整距离，点击画面可重新对焦。");
        }
    }

    private void Analyze(IImageProxy image, FocusedCameraBarcodeReaderView view, int generation, FrameMetrics metrics)
    {
        var frameSucceeded = false;
        try
        {
            if (generation != Volatile.Read(ref _generation) || !view.IsDetecting) return;
            if (RecordFrameArrival(metrics))
                Log.Information("Scanner first frame: ElapsedMs={ElapsedMs}", _sessionWatch.ElapsedMilliseconds);

            var plane = image.GetPlanes()[0];
            var buffer = plane.Buffer;
            if (Interlocked.Exchange(ref metrics.DetailsPending, 0) != 0)
                Log.Information("Scanner frame layout: Revision={Revision}, Generation={Generation}, Layout={Layout}",
                    ScannerRevision, generation, DescribeFrame(image));
            var pixels = new byte[checked(image.Width * image.Height)];
            Java.Nio.ByteBuffer? packed = null;
            try
            {
                using var duplicate = buffer.Duplicate();
                duplicate.Rewind();
                // Account for padded Y-plane rows on PDA cameras. ZXing expects tightly packed luminance.
                if (plane.RowStride != image.Width || plane.PixelStride != 1)
                {
                    var sourceLength = checked((image.Height - 1) * plane.RowStride + (image.Width - 1) * plane.PixelStride + 1);
                    var source = new byte[sourceLength];
                    if (duplicate.Remaining() < sourceLength)
                        throw new InvalidDataException($"相机亮度数据不足：需要 {sourceLength} 字节，实际剩余 {duplicate.Remaining()} 字节。");
                    // One native bulk read avoids a JNI call for every pixel on padded frames.
                    duplicate.Get(source);
                    CameraLuminanceBuffer.Copy(source, pixels, image.Width, image.Height, plane.RowStride, plane.PixelStride);
                    packed = Java.Nio.ByteBuffer.Wrap(pixels);
                }
                else
                {
                    if (duplicate.Remaining() < pixels.Length)
                        throw new InvalidDataException($"相机亮度数据不足：需要 {pixels.Length} 字节，实际剩余 {duplicate.Remaining()} 字节。");
                    duplicate.Get(pixels);
                }
                var frame = new PixelBufferHolder { Data = packed ?? buffer, Size = new Size(image.Width, image.Height) };
                ((ICameraFrameAnalyzer)view).FrameReady(new CameraFrameBufferEventArgs(frame));
                BarcodeResult[]? results;
                lock (metrics.DecoderLock)
                {
                    if (generation != Volatile.Read(ref _generation)) return;
                    var options = view.Options;
                    if (metrics.Decoder is null || metrics.DecoderOptions != options)
                    {
                        // ZXing.Net.Maui 0.4.0 uses the same individual format flag values as ZXing.Net.
                        var formats = Enum.GetValues<BarcodeFormat>()
                            .Where(format => (options.Formats & format) != 0)
                            .Select(format => (ZXing.BarcodeFormat)(int)format).ToList();
                        metrics.Decoder = new CameraBarcodeDecoder(formats, options.AutoRotate, options.TryHarder,
                            options.TryInverted, options.Multiple);
                        metrics.DecoderOptions = options;
                    }
                    results = metrics.Decoder.Decode(pixels, image.Width, image.Height, image.ImageInfo.RotationDegrees)?.ToBarcodeResults();
                }
                if (generation != Volatile.Read(ref _generation)) return;
                Interlocked.Increment(ref metrics.Processed);
                if (results?.Length > 0)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (generation != _generation || !_connected || !view.IsDetecting) return;
                        Log.Information("Scanner decoded: ElapsedMs={ElapsedMs}", _sessionWatch.ElapsedMilliseconds);
                        ((ICameraBarcodeReaderView)view).BarcodesDetected(new BarcodeDetectionEventArgs(results));
                    });
                }
            }
            finally { packed?.Dispose(); }
            frameSucceeded = true;
        }
        catch (Exception ex) { RecordFrameFailure(ex, generation, DescribeFrame(image), metrics); }
        finally
        {
            try { image.Close(); }
            catch (Exception ex)
            {
                frameSucceeded = false;
                RecordFrameFailure(ex, generation, "image.Close", metrics);
            }
            if (frameSucceeded) Interlocked.Exchange(ref metrics.ConsecutiveErrors, 0);
            Volatile.Write(ref metrics.LastCompletion, Stopwatch.GetTimestamp());
        }
    }

    private void CancelFocus()
    {
        _focusRetries?.Cancel();
        _focusRetries?.Dispose();
        _focusRetries = null;
        CancelFocusTimeout();
        var future = _focusFuture;
        _focusFuture = null;
        try
        {
            future?.Cancel(true);
            _camera?.CameraControl.CancelFocusAndMetering();
        }
        catch (Exception ex) { Log.Warning(ex, "Scanner focus cancellation unavailable"); }
    }

    private void StopCamera(bool resetRecoveryBudget = true)
    {
        Interlocked.Increment(ref _generation);
        _starting = false;
        _streaming = false;
        StopOrientationTracking();
        StopCameraDiagnostics();
        CancelFocus();
        if (_previewObserver is not null)
        {
            PlatformView.PreviewStreamState.RemoveObserver(_previewObserver);
            _previewObserver.Dispose();
            _previewObserver = null;
        }
        try
        {
            _analysis?.ClearAnalyzer();
            // Release only this scanner's use cases, preserving other camera consumers.
            var useCases = new List<UseCase>();
            if (_preview is not null) useCases.Add(_preview);
            if (_analysis is not null) useCases.Add(_analysis);
            if (useCases.Count > 0) _provider?.Unbind(useCases.ToArray());
        }
        catch (Exception ex) { Log.Warning(ex, "Scanner camera release failed"); }
        _camera = null;
        _preview?.Dispose();
        _preview = null;
        _analysis?.Dispose();
        _analysis = null;
        _executor?.Shutdown();
        _executor?.Dispose();
        _executor = null;
        // The executor can still be finishing a frame; its analyzer is collected after that callback.
        _analyzer = null;
        _lifecycleOwner = null;
        if (resetRecoveryBudget)
        {
            _recoveryPolicy.Reset();
            _recoveryLimitReported = false;
        }
    }

    private sealed class FrameAnalyzer(Action<IImageProxy> analyze) : Java.Lang.Object, ImageAnalysis.IAnalyzer
    {
        public void Analyze(IImageProxy image) => analyze(image);
    }

    private sealed class PreviewObserver(Action<bool> changed) : Java.Lang.Object, IObserver
    {
        public void OnChanged(Java.Lang.Object? value) => changed(PreviewView.StreamState.Streaming!.Equals(value));
    }
}
