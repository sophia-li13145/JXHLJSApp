using JXHLJSApp.Models.WorkOrders;
using JXHLJSApp.Services;
using JXHLJSApp.Services.WorkOrders;

namespace JXHLJSApp.Pages.WorkStart;

[QueryProperty(nameof(WorkOrderId), "id")]
[QueryProperty(nameof(WorkOrderNo), "workOrderNo")]
public partial class MaterialUnloadingPage : ContentPage
{
    private readonly IWorkOrderApi _workOrderApi;
    private readonly IScanService _scanService;
    private string? _workOrderId;
    private string? _workOrderNo;
    private bool _machineConfirmed;
    private bool _isBusy;
    private string? _lastMaterialQrCode;
    private MaterialOutputConfirmDto? _confirmOutput;
    private WorkOrderInputOutputDto? _inputOutput;

    public string? WorkOrderId
    {
        get => _workOrderId;
        set => _workOrderId = Uri.UnescapeDataString(value ?? string.Empty);
    }

    public string? WorkOrderNo
    {
        get => _workOrderNo;
        set => _workOrderNo = Uri.UnescapeDataString(value ?? string.Empty);
    }

    public MaterialUnloadingPage(IWorkOrderApi workOrderApi, IScanService scanService)
    {
        InitializeComponent();
        _workOrderApi = workOrderApi;
        _scanService = scanService;
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnScanPanelTapped(object sender, TappedEventArgs e)
    {
        if (_isBusy) return;

        if (!_machineConfirmed)
        {
            await ScanMachineAsync();
            return;
        }

        await ScanMaterialAsync();
    }

    private async Task ScanMachineAsync()
    {
        var code = await _scanService.ScanAsync("扫描机台二维码");
        if (string.IsNullOrWhiteSpace(code)) return;

        await ConfirmMachineAsync(code);
    }

    private async void OnManualMachineConfirmClicked(object sender, EventArgs e)
    {
        if (_isBusy) return;

        await ConfirmMachineAsync(MachineCodeEntry.Text);
    }

    private async void OnManualMachineCompleted(object sender, EventArgs e)
    {
        if (_isBusy) return;

        await ConfirmMachineAsync(MachineCodeEntry.Text);
    }

    private async Task ConfirmMachineAsync(string? machineCode)
    {
        var devCode = machineCode?.Trim();
        if (string.IsNullOrWhiteSpace(devCode))
        {
            await DisplayAlert("提示", "请输入机台码", "确定");
            return;
        }

        if (string.IsNullOrWhiteSpace(_workOrderNo))
        {
            await DisplayAlert("提示", "工单号为空，无法扫码下料。", "确定");
            return;
        }

        try
        {
            _isBusy = true;
            var result = await _workOrderApi.ScanToWorkAsync(devCode, _workOrderNo);
            if (!result)
            {
                await DisplayAlert("识别失败", "机台识别未成功，请确认机台码后重试。", "确定");
                return;
            }

            _machineConfirmed = true;
            MachineCodeEntry.Text = devCode;
            ShowMaterialScanStep();
        }
        catch (Exception ex)
        {
            await DisplayAlert("识别失败", ex.Message, "确定");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task ScanMaterialAsync()
    {
        var code = await _scanService.ScanAsync("扫描下料标签二维码");
        if (string.IsNullOrWhiteSpace(code)) return;

        try
        {
            _isBusy = true;
            _lastMaterialQrCode = code.Trim();
            var material = await _workOrderApi.ScanQueryMaterialInfoAsync(_lastMaterialQrCode);
            var inputOutputs = string.IsNullOrWhiteSpace(_workOrderNo)
                ? new List<WorkOrderInputOutputDto>()
                : await _workOrderApi.GetWorkOrderInputOutputAsync(_workOrderNo);
            _inputOutput = inputOutputs.FirstOrDefault();
            BindMaterial(material, _inputOutput);
            ShowResultStep();
        }
        catch (Exception ex)
        {
            await DisplayAlert("扫码失败", ex.Message, "确定");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void ShowMaterialScanStep()
    {
        ScanIconLabel.Text = "📦";
        ScanTitleLabel.Text = "2. 机台识别确认";
        ScanHintLabel.Text = "点击扫描下料标签二维码";
        ManualMachinePanel.IsVisible = false;
    }

    private void ShowResultStep()
    {
        ScanPanel.IsVisible = false;
        SuccessBanner.IsVisible = true;
        ResultCard.IsVisible = true;
    }

    private void BindMaterial(MaterialQrCodeInfoDto material, WorkOrderInputOutputDto? inputOutput)
    {
        var outputLength = material.length ?? TryParseDecimal(inputOutput?.wireTakeUpLength);
        var pieceWeight = material.weight;
        _confirmOutput = new MaterialOutputConfirmDto
        {
            outputLength = outputLength,
            pieceWeight = pieceWeight,
            productInspectStatus = "合格品",
            qrCode = string.IsNullOrWhiteSpace(material.qrCode) ? _lastMaterialQrCode : material.qrCode,
            workOrderNo = _workOrderNo
        };

        InspectStatusLabel.Text = _confirmOutput.productInspectStatus;
        OutputLengthLabel.Text = FormatDecimal(outputLength);
        PieceWeightLabel.Text = FormatDecimal(pieceWeight);
        MaterialCodeLabel.Text = ValueOrDash(FirstNonEmpty(inputOutput?.outputMaterialCode, material.materialCode));
        SteelGradeLabel.Text = ValueOrDash(inputOutput?.machineNo);
        OriginPlaceLabel.Text = ValueOrDash(FirstNonEmpty(inputOutput?.customerCode, inputOutput?.outputOriginPlace, material.originPlace));
        SpecLabel.Text = ValueOrDash(FirstNonEmpty(inputOutput?.outputSpecification, material.specification, material.spec));
        WeightLabel.Text = FormatDecimalWithUnit(pieceWeight, FirstNonEmpty(material.weightUnit, material.unit, "KG"));
        LengthLabel.Text = FormatDecimalWithUnit(outputLength, FirstNonEmpty(material.lengthUnit, "m"));
    }

    private async void OnConfirmBackClicked(object sender, EventArgs e)
    {
        if (_isBusy) return;

        if (_confirmOutput is null)
        {
            await DisplayAlert("提示", "请先扫描下料标签二维码。", "确定");
            return;
        }

        if (string.IsNullOrWhiteSpace(_confirmOutput.qrCode))
        {
            await DisplayAlert("提示", "二维码为空，无法确认下料。", "确定");
            return;
        }

        if (string.IsNullOrWhiteSpace(_confirmOutput.workOrderNo))
        {
            await DisplayAlert("提示", "工单号为空，无法确认下料。", "确定");
            return;
        }

        try
        {
            _isBusy = true;
            ConfirmButton.IsEnabled = false;
            var result = await _workOrderApi.ConfirmMaterialOutputAsync(_confirmOutput);
            if (!result)
            {
                await DisplayAlert("确认失败", "接口返回下料确认失败，请稍后重试。", "确定");
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await DisplayAlert("确认失败", ex.Message, "确定");
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

    private static string FormatDecimalWithUnit(decimal? value, string? unit)
    {
        if (!value.HasValue) return "--";

        var text = value.Value % 1 == 0 ? value.Value.ToString("0") : value.Value.ToString("0.##");
        return string.IsNullOrWhiteSpace(unit) ? text : $"{text} {unit}";
    }

    private async void OnReworkReportClicked(object sender, EventArgs e) => await GoReportAsync(AppShell.RouteReworkReport);

    private async void OnAbnormalReportClicked(object sender, EventArgs e) => await GoReportAsync(AppShell.RouteAbnormalReport);

    private async Task GoReportAsync(string route)
    {
        var query = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(_workOrderNo))
        {
            query["workOrderNo"] = _workOrderNo;
        }

        await Shell.Current.GoToAsync(route, query);
    }

    private static string FormatDecimal(decimal? value) => value.HasValue ? value.Value.ToString("0.##") : "--";

    private static decimal? TryParseDecimal(string? value) =>
        decimal.TryParse(value, out var result) ? result : null;
}
