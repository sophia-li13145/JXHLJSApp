using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using JXHLJSApp.Models.WorkOrders;
using JXHLJSApp.Services;
using JXHLJSApp.Services.WorkOrders;

namespace JXHLJSApp.Pages.WorkStart;

public partial class MaterialLoadingPage : ContentPage
{
    private readonly IWorkOrderApi _workOrderApi;
    private readonly CameraScanService _cameraScanner;
    private bool _isHandlingScan;
    private readonly IProductionContextService _productionContext;
    private bool _machineConfirmed;
    private bool _isBusy;
    private string? _lastMaterialQrCode;
    private MaterialInputConfirmDto? _confirmInput;


    public MaterialLoadingPage(IWorkOrderApi workOrderApi, CameraScanService cameraScanner, IRightKeyScanService scanner, IProductionContextService productionContext)
    {
        InitializeComponent();
        _workOrderApi = workOrderApi;
        _cameraScanner = cameraScanner;
        scanner.Register(this,
            code => _machineConfirmed ? ScanMaterialAsync(code) : ScanMachineAsync(code),
            () => !_isHandlingScan && !_isBusy && _confirmInput is null);
        _productionContext = productionContext;
        ReuseConfirmedPicklingMachine();
    }

    private void ReuseConfirmedPicklingMachine()
    {
        var current = _productionContext.Current;
        if (current?.IsPicklingMachineConfirmed == true &&
            IsPicklingProcess(current.OperationName) &&
            !string.IsNullOrWhiteSpace(current.MachineCode))
        {
            _machineConfirmed = true;
            ShowMaterialScanStep();
        }
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnScanPanelClicked(object sender, EventArgs e)
    {
        if (_isBusy) return;

        if (!_machineConfirmed)
        {
            await ScanMachineAsync();
            return;
        }

        await ScanMaterialAsync();
    }

    private async Task ScanMachineAsync(string? scanCode = null)
    {
        if (_isHandlingScan || _isBusy || _confirmInput is not null) return;

        _isHandlingScan = true;
        try
        {
            scanCode ??= await _cameraScanner.ScanAsync(this, "扫描机台二维码");
            if (string.IsNullOrWhiteSpace(scanCode)) return;
            var code = scanCode;

            await ConfirmMachineAsync(code);
        }
        finally
        {
            _isHandlingScan = false;
        }
    }

    private async Task ConfirmMachineAsync(string? machineCode)
    {
        var devCode = machineCode?.Trim();
        if (string.IsNullOrWhiteSpace(devCode))
        {
            await DisplayAlert("提示", "请输入机台码", "确定");
            return;
        }

        var workOrderNo = _productionContext.Current?.WorkOrderNo;
        if (string.IsNullOrWhiteSpace(workOrderNo))
        {
            await DisplayAlert("提示", "当前生产工单为空，无法扫码开工。", "确定");
            return;
        }

        try
        {
            _isBusy = true;
            var result = await _workOrderApi.ScanToWorkAsync(devCode, workOrderNo);
            if (!result)
            {
                await ErrorDialogService.ShowAsync(this, "识别失败", "机台识别未成功，请确认机台码后重试。", "确定");
                return;
            }

            _machineConfirmed = true;
            UpdateProductionContextMachine(devCode);
            ShowMaterialScanStep();
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "识别失败", ex.Message, "确定");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void UpdateProductionContextMachine(string machineCode)
    {
        var current = _productionContext.Current;
        if (current is not null)
        {
            _productionContext.Set(new ProductionContext
            {
                WorkOrderId = current.WorkOrderId,
                WorkOrderNo = current.WorkOrderNo,
                OperationName = current.OperationName,
                ExecutionId = current.ExecutionId,
                MachineCode = machineCode,
                IsPicklingMachineConfirmed = current.IsPicklingMachineConfirmed || IsPicklingProcess(current.OperationName),
                Status = current.Status,
                StartedAt = current.StartedAt,
                SessionId = current.SessionId
            });
            return;
        }


    }

    private async Task ScanMaterialAsync(string? scanCode = null)
    {
        if (_isHandlingScan || _isBusy || _confirmInput is not null) return;

        _isHandlingScan = true;
        try
        {
            scanCode ??= await _cameraScanner.ScanAsync(this, "扫描上料物料二维码");
            if (string.IsNullOrWhiteSpace(scanCode)) return;
            var code = scanCode;

            try
            {
                _isBusy = true;
                _lastMaterialQrCode = code.Trim();
                var material = await _workOrderApi.ScanQueryMaterialInfoAsync(_lastMaterialQrCode);
                if (string.IsNullOrWhiteSpace(material?.materialCode))
                {
                    throw new InvalidOperationException("未查询到有效的上料物料，请重新扫描物料二维码。");
                }
                BindMaterial(material);
                ShowResultStep();
            }
            catch (Exception ex)
            {
                _confirmInput = null;
                _lastMaterialQrCode = null;
                ScanPanel.IsVisible = true;
                SuccessBanner.IsVisible = false;
                ResultCard.IsVisible = false;
                ShowMaterialScanStep();
                await ErrorDialogService.ShowAsync(this, "扫码失败", ex.Message, "确定");
            }
            finally
            {
                _isBusy = false;
            }
        }
        finally
        {
            _isHandlingScan = false;
        }
    }

    private void ShowMaterialScanStep()
    {
        ScanIconLabel.Text = "📦";
        ScanTitleLabel.Text = "2. 摄像头扫描上料物料码";
        ScanHintLabel.Text = $"已验证机台 {_productionContext.Current?.MachineCode}";
    }

    private void ShowResultStep()
    {
        ScanPanel.IsVisible = false;
        SuccessBanner.IsVisible = true;
        ResultCard.IsVisible = true;
    }

    private void BindMaterial(MaterialQrCodeInfoDto material)
    {
        _confirmInput = new MaterialInputConfirmDto
        {
            materialCode = material.materialCode,
            materialName = material.materialName,
            qrCode = string.IsNullOrWhiteSpace(material.qrCode) ? _lastMaterialQrCode : material.qrCode,
            spec = FirstNonEmpty(material.specification, material.spec),
            workOrderNo = _productionContext.Current?.WorkOrderNo
        };

        MaterialCodeLabel.Text = ValueOrDash(material.materialCode);
        SteelGradeLabel.Text = ValueOrDash(material.steelGrade);
        OriginPlaceLabel.Text = ValueOrDash(material.originPlace);
        SpecLabel.Text = ValueOrDash(_confirmInput.spec);
        var (pieceWeight, pieceWeightUnit) = NormalizePieceWeightForDisplay(material.weight, material.unit);
        WeightLabel.Text = FormatDecimalWithUnit(pieceWeight, pieceWeightUnit);
        LengthLabel.Text = FormatDecimalWithUnit(material.length, FirstNonEmpty(material.lengthUnit, "m"));
    }

    private async void OnConfirmBackClicked(object sender, EventArgs e)
    {
        if (_isBusy) return;

        if (_confirmInput is null)
        {
            await DisplayAlert("提示", "请先扫描上料物料二维码。", "确定");
            return;
        }

        if (string.IsNullOrWhiteSpace(_confirmInput.materialCode))
        {
            await DisplayAlert("提示", "物料编号为空，无法确认上料。", "确定");
            return;
        }

        if (string.IsNullOrWhiteSpace(_confirmInput.qrCode))
        {
            await DisplayAlert("提示", "二维码为空，无法确认上料。", "确定");
            return;
        }

        if (string.IsNullOrWhiteSpace(_confirmInput.workOrderNo))
        {
            await DisplayAlert("提示", "工单号为空，无法确认上料。", "确定");
            return;
        }

        try
        {
            _isBusy = true;
            ConfirmButton.IsEnabled = false;
            var result = await _workOrderApi.ConfirmMaterialInputAsync(_confirmInput);
            if (!result)
            {
                await ErrorDialogService.ShowAsync(this, "确认失败", "接口返回上料确认失败，请稍后重试。", "确定");
                return;
            }

            // 成功只做轻提示，不再进入“操作提示（操作成功）”页，直接回到上一层“生产执行中”页面。
            try
            {
                await Toast.Make("上料成功", ToastDuration.Short).Show();
            }
            catch
            {
                // 轻提示失败不影响返回执行页。
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "确认失败", ex.Message, "确定");
        }
        finally
        {
            _isBusy = false;
            ConfirmButton.IsEnabled = true;
        }
    }

    private static string ValueOrDash(string? value) => string.IsNullOrWhiteSpace(value) ? "--" : value;

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static (decimal? value, string unit) NormalizePieceWeightForDisplay(decimal? value, string? unit)
    {
        if (IsTonUnit(unit))
        {
            return (value * 1000m, "KG");
        }

        return (value, "KG");
    }

    private static bool IsTonUnit(string? unit) =>
        string.Equals(unit?.Trim(), "吨", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(unit?.Trim(), "t", StringComparison.OrdinalIgnoreCase);

    private static bool IsPicklingProcess(string? operationName) =>
        operationName?.Contains("酸洗", StringComparison.OrdinalIgnoreCase) == true;

    private static string FormatDecimalWithUnit(decimal? value, string? unit)
    {
        if (!value.HasValue) return "--";

        var text = value.Value % 1 == 0 ? value.Value.ToString("0") : value.Value.ToString("0.##");
        return string.IsNullOrWhiteSpace(unit) ? text : $"{text} {unit}";
    }
}
