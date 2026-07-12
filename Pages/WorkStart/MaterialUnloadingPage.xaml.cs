using JXHLJSApp.Models.WorkOrders;
using JXHLJSApp.Services;
using JXHLJSApp.Services.WorkOrders;
using System.Globalization;
using System.Text.RegularExpressions;

namespace JXHLJSApp.Pages.WorkStart;

public partial class MaterialUnloadingPage : ContentPage
{
    private readonly IWorkOrderApi _workOrderApi;
    private readonly IScanService _scanService;
    private readonly IProductionContextService _productionContext;
    private bool _machineConfirmed;
    private bool _isBusy;
    private string? _lastMaterialQrCode;
    private MaterialOutputConfirmDto? _confirmOutput;
    private WorkOrderInputOutputDto? _inputOutput;


    public MaterialUnloadingPage(IWorkOrderApi workOrderApi, IScanService scanService, IProductionContextService productionContext)
    {
        InitializeComponent();
        _workOrderApi = workOrderApi;
        _scanService = scanService;
        _productionContext = productionContext;
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

        var workOrderNo = _productionContext.Current?.WorkOrderNo;
        if (string.IsNullOrWhiteSpace(workOrderNo))
        {
            await DisplayAlert("提示", "当前生产工单为空，无法扫码下料。", "确定");
            return;
        }

        try
        {
            _isBusy = true;
            var result = await _workOrderApi.ScanToWorkAsync(devCode, workOrderNo);
            if (!result)
            {
                await DisplayAlert("识别失败", "机台识别未成功，请确认机台码后重试。", "确定");
                return;
            }

            _machineConfirmed = true;
            MachineCodeEntry.Text = devCode;
            UpdateProductionContextMachine(devCode);
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

    private void UpdateProductionContextMachine(string machineCode)
    {
        var current = _productionContext.Current;
        if (current is not null)
        {
            _productionContext.Set(new ProductionContext
            {
                WorkOrderId = current.WorkOrderId,
                WorkOrderNo = current.WorkOrderNo,
                ExecutionId = current.ExecutionId,
                MachineCode = machineCode,
                Status = current.Status,
                StartedAt = current.StartedAt,
                SessionId = current.SessionId
            });
            return;
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
            var workOrderNo = _productionContext.Current?.WorkOrderNo;
            var inputOutputs = string.IsNullOrWhiteSpace(workOrderNo)
                ? new List<WorkOrderInputOutputDto>()
                : await _workOrderApi.GetWorkOrderInputOutputAsync(workOrderNo);
            _inputOutput = inputOutputs.FirstOrDefault();
            BindInputOutput(_inputOutput);
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

    private void BindInputOutput(WorkOrderInputOutputDto? inputOutput)
    {
        var outputLength = TryParseDecimal(inputOutput?.wireTakeUpLength);
        _confirmOutput = new MaterialOutputConfirmDto
        {
            outputLength = outputLength,
            qrCode = _lastMaterialQrCode,
            workOrderNo = _productionContext.Current?.WorkOrderNo
        };

        OutputLengthLabel.Text = $"产出长度（m）（收线长度：{FormatDecimalWithUnit(outputLength, "m")}）*";
        OutputLengthEntry.Text = FormatDecimalInput(outputLength);
        InputMaterialCodeLabel.Text = $"物料: {ValueOrDash(inputOutput?.inputMaterialCode)}";
        InputSteelGradeLabel.Text = $"钢号: {ValueOrDash(FirstNonEmpty(inputOutput?.inputSteel, inputOutput?.inputSteelGrade, inputOutput?.inputMaterialName))}";
        InputOriginPlaceLabel.Text = $"产地: {ValueOrDash(inputOutput?.inputOriginPlace)}";
        InputSpecLabel.Text = $"规格: {ValueOrDash(inputOutput?.inputSpecification)}";
        MaterialCodeLabel.Text = $"物料: {ValueOrDash(inputOutput?.outputMaterialCode)}";
        SteelGradeLabel.Text = $"钢号: {ValueOrDash(FirstNonEmpty(inputOutput?.outputSteel, inputOutput?.outputSteelGrade, inputOutput?.outputMaterialName))}";
        OriginPlaceLabel.Text = $"产地: {ValueOrDash(inputOutput?.outputOriginPlace)}";
        SpecLabel.Text = $"规格: {ValueOrDash(inputOutput?.outputSpecification)}";
        OutputWorkOrderNoLabel.Text = $"生产者 {ValueOrDash(FirstNonEmpty(inputOutput?.workOrderNo, _productionContext.Current?.WorkOrderNo))}";
        OutputMachineLabel.Text = $"机台 {ValueOrDash(FirstNonEmpty(inputOutput?.machineNo, inputOutput?.machineType, inputOutput?.deviceName, _productionContext.Current?.MachineCode))}";
        OutputCustomerCodeLabel.Text = $"客户代码 {ValueOrDash(inputOutput?.customerCode)}";
        OutputSequenceLabel.Text = $"当前序号 {FormatDecimal(inputOutput?.currentSequenceNo)}";
        RecalculateOutputFields();
    }

    private async void OnConfirmBackClicked(object sender, EventArgs e)
    {
        if (_isBusy) return;

        if (_confirmOutput is null)
        {
            await DisplayAlert("提示", "请先扫描下料标签二维码。", "确定");
            return;
        }

        ApplyManualOutputValues();

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

        if (string.IsNullOrWhiteSpace(_confirmOutput.productInspectStatus))
        {
            await DisplayAlert("提示", "产品检验状态为空，请检查产出长度。", "确定");
            return;
        }

        if (!_confirmOutput.outputLength.HasValue)
        {
            await DisplayAlert("提示", "请输入产出长度。", "确定");
            return;
        }

        if (!_confirmOutput.pieceWeight.HasValue)
        {
            await DisplayAlert("提示", "件重计算失败，请检查产出长度和规格。", "确定");
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
        await Shell.Current.GoToAsync(route);
    }

    private static string FormatDecimal(decimal? value) => value.HasValue ? value.Value.ToString("0.##") : "--";

    private static string FormatDecimalInput(decimal? value) => value.HasValue ? value.Value.ToString("0.##") : string.Empty;

    private static decimal? TryParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var match = Regex.Match(value, @"[-+]?\d+(?:[.,]\d+)?");
        if (!match.Success) return null;

        var normalized = match.Value.Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private void OnManualOutputTextChanged(object sender, TextChangedEventArgs e) => RecalculateOutputFields();

    private void OnManualOutputCompleted(object sender, EventArgs e) => ApplyManualOutputValues();

    private void ApplyManualOutputValues() => RecalculateOutputFields();

    private void RecalculateOutputFields()
    {
        if (_confirmOutput is null)
        {
            return;
        }

        var outputLength = TryParseDecimal(OutputLengthEntry.Text);
        var pieceWeight = CalculatePieceWeight(outputLength, _inputOutput?.outputSpecification);
        var receiveLength = TryParseDecimal(_inputOutput?.wireTakeUpLength);
        var inspectStatus = outputLength.HasValue && receiveLength.HasValue && receiveLength.Value >= outputLength.Value ? "合格品" : "小件";

        _confirmOutput.outputLength = outputLength;
        _confirmOutput.pieceWeight = pieceWeight;
        _confirmOutput.productInspectStatus = inspectStatus;

        InspectStatusEntry.Text = inspectStatus;
        InspectStatusEntry.TextColor = inspectStatus == "合格品" ? Color.FromArgb("#00A86B") : Color.FromArgb("#D97706");
        PieceWeightEntry.Text = FormatDecimalInput(pieceWeight);
        WeightLabel.Text = $"件重: {FormatDecimalWithUnit(pieceWeight, "KG")}";
        LengthLabel.Text = $"长度: {FormatDecimalWithUnit(outputLength, "m")}";
    }

    private static decimal? CalculatePieceWeight(decimal? outputLength, string? outputSpecification)
    {
        var spec = TryParseDecimal(outputSpecification);
        return outputLength.HasValue && spec.HasValue
            ? Math.Round(outputLength.Value * 0.00617m * spec.Value * spec.Value, 2)
            : null;
    }
}
