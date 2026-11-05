using JXHLJSApp.Models.Quality;
using JXHLJSApp.Services;

namespace JXHLJSApp.Pages.Quality;

public partial class QualityCodeConvertPage : ContentPage, IQueryAttributable
{
    private readonly IQualityCodeConvertApi _api;
    private readonly IRightKeyScanService _scanner;
    private QualityCodeMaterialDto? _material;
    private QualityCodeConvertResultDto? _lastConversion;
    private string? _lastTargetQrCode;
    private string? _lastMaterialName;
    private bool _isConfirmingCancel;
    private bool _isHandlingScan;
    private CancellationTokenSource _navigationCts = new();

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("cancelledQualityCodes", out var value) && value is string[] codes &&
            _lastTargetQrCode is not null && codes.Contains(_lastTargetQrCode))
            ShowCancelled();
        query.Remove("cancelledQualityCodes");
    }

    public QualityCodeConvertPage(IQualityCodeConvertApi api, IRightKeyScanService scanner)
    {
        InitializeComponent();
        _api = api;
        _scanner = scanner;
        _scanner.Register(this, code => ScanAsync(code), () => !_isHandlingScan && !_isConfirmingCancel);
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (_navigationCts.IsCancellationRequested)
        {
            _navigationCts.Dispose();
            _navigationCts = new CancellationTokenSource();
        }
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        // An error dialog does not end the current business operation.
        if (!Navigation.ModalStack.Any()) _navigationCts.Cancel();
    }

    private async Task ScanAsync(string? scanCode = null)
    {
        if (string.IsNullOrWhiteSpace(scanCode))
        {
            await _scanner.ShowHintAsync(this, _material is null ? "扫描原物料码" : "扫描空白质检码");
            return;
        }

        if (_isHandlingScan || _isConfirmingCancel) return;
        _isHandlingScan = true;
        SetBusy(true);
        var ct = _navigationCts.Token;
        try
        {
            var code = scanCode.Trim();
            if (string.IsNullOrWhiteSpace(code)) return;
            ct.ThrowIfCancellationRequested();

            if (_material is not null)
            {
                LoadingIndicator.IsVisible = LoadingIndicator.IsRunning = true;
                var result = await _api.TransCodeAsync(_material.qrCode!, code, ct);
                _lastConversion = result;
                _lastTargetQrCode = code;
                _lastMaterialName = _material.materialName;
                _material = null;
                SuccessQrCode.Text = result.qrCode;
                SuccessCard.IsVisible = true;
                CancelledBanner.IsVisible = false;
                RenderStep();
                await PageScroll.ScrollToAsync(0, 0, false);
                return;
            }

            LoadingIndicator.IsVisible = LoadingIndicator.IsRunning = true;
            var material = await _api.ScanMaterialAsync(code, ct);
            ct.ThrowIfCancellationRequested();
            _material = material;
            CancelledBanner.IsVisible = false;
            RenderStep();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
                await ErrorDialogService.ShowAsync(this, _material is null ? "扫码查询失败" : "转码失败", ex.Message, "确定");
        }
        finally
        {
            LoadingIndicator.IsVisible = LoadingIndicator.IsRunning = false;
            _isHandlingScan = false;
            SetBusy(false);
        }
    }

    private void RenderStep()
    {
        var ready = _material is not null;
        StepTitle.Text = ready ? "再扫空白质检码" : "先扫原物料码";
        StepHint.Text = ready ? "扫完自动成功，不用点确认" : "就是大件物料上的二维码";
        MaterialCard.BackgroundColor = ready ? Colors.White : Color.FromArgb("#EDF5FF");
        MaterialCard.StrokeThickness = ready ? 0 : 2;
        MaterialBadge.BackgroundColor = Color.FromArgb(ready ? "#E0F2EA" : "#244B88");
        MaterialNumber.Text = ready ? "✓" : "1";
        MaterialNumber.TextColor = ready ? Color.FromArgb("#109982") : Colors.White;
        MaterialDescription.Text = ready
            ? string.Join(" · ", new[] { _material?.materialName, _material?.spec }.Where(x => !string.IsNullOrWhiteSpace(x)))
            : "等你扫码";
        if (ready && string.IsNullOrWhiteSpace(MaterialDescription.Text)) MaterialDescription.Text = "物料信息已读取";
        MaterialCode.Text = _material?.qrCode;
        MaterialCode.IsVisible = ready;
        QualityCard.BackgroundColor = ready ? Color.FromArgb("#EDF5FF") : Colors.White;
        QualityCard.Stroke = Color.FromArgb("#244B88");
        QualityCard.StrokeThickness = ready ? 2 : 0;
        QualityBadge.BackgroundColor = Color.FromArgb(ready ? "#244B88" : "#E9EEF5");
        QualityNumber.TextColor = ready ? Colors.White : Color.FromArgb("#7B8EA8");
        QualityHint.Text = ready ? "扫要贴在样品上的新标签" : "先扫上面的原物料码";
        ScanButton.Text = ready ? "按右侧键扫空白质检码" : "按右侧键扫原物料码";
        RescanButton.IsVisible = ready;
    }

    private void SetBusy(bool busy)
    {
        ScanButton.IsEnabled = RescanButton.IsEnabled = !busy;
        CancelTransCodeButton.IsEnabled = !busy;
        KeepTransCodeButton.IsEnabled = ConfirmCancelButton.IsEnabled = !busy;
    }

    private void OnCancelTransCodeClicked(object sender, EventArgs e)
    {
        if (_isHandlingScan || _lastConversion is null || string.IsNullOrWhiteSpace(_lastTargetQrCode)) return;
        CancelMaterialName.Text = string.IsNullOrWhiteSpace(_lastMaterialName) ? "物料名称未提供" : _lastMaterialName;
        CancelQualityCode.Text = $"质检码：{_lastConversion.qrCode ?? _lastTargetQrCode}";
        SetCancelConfirmation(true);
    }

    private void SetCancelConfirmation(bool visible)
    {
        _isConfirmingCancel = visible;
        PageScroll.IsVisible = !visible;
        CancelConfirmationScroll.IsVisible = visible;
    }

    private void OnKeepTransCodeClicked(object sender, EventArgs e)
    {
        if (!_isHandlingScan) SetCancelConfirmation(false);
    }

    private async void OnConfirmCancelClicked(object sender, EventArgs e)
    {
        if (_isHandlingScan || !_isConfirmingCancel || _lastConversion is null || string.IsNullOrWhiteSpace(_lastTargetQrCode)) return;
        _isHandlingScan = true;
        SetBusy(true);
        CancelLoadingIndicator.IsVisible = CancelLoadingIndicator.IsRunning = true;
        var ct = _navigationCts.Token;
        try
        {
            await _api.CancelByTargetQrCodeAsync(_lastTargetQrCode, ct);
            ShowCancelled();
            SetCancelConfirmation(false);
            await PageScroll.ScrollToAsync(0, 0, false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
                await ErrorDialogService.ShowAsync(this, "取消转码失败", ex.Message, "确定");
        }
        finally
        {
            CancelLoadingIndicator.IsVisible = CancelLoadingIndicator.IsRunning = false;
            _isHandlingScan = false;
            SetBusy(false);
        }
    }

    private async void OnScanClicked(object sender, EventArgs e) => await ScanAsync();

    private void OnRescanClicked(object sender, EventArgs e)
    {
        if (_isHandlingScan) return;
        _material = null;
        RenderStep();
    }

    protected override bool OnBackButtonPressed()
    {
        if (!_isConfirmingCancel) return base.OnBackButtonPressed();
        if (!_isHandlingScan) SetCancelConfirmation(false);
        return true;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        if (_isConfirmingCancel)
        {
            if (!_isHandlingScan) SetCancelConfirmation(false);
            return;
        }
        await Shell.Current.GoToAsync("..");
    }

    private void ShowCancelled()
    {
        _lastConversion = null;
        _lastTargetQrCode = null;
        _lastMaterialName = null;
        _material = null;
        SuccessCard.IsVisible = false;
        CancelledBanner.IsVisible = true;
        RenderStep();
    }

    private async void OnRecordsClicked(object sender, EventArgs e)
    {
        if (_isHandlingScan || _isConfirmingCancel) return;
        await Shell.Current.GoToAsync(AppShell.RouteQualityCodeRecords);
    }
}
