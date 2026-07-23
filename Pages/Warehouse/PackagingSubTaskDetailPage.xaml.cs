using System.Globalization;
using JXHLJSApp.Models.Warehouse;
using JXHLJSApp.Models.WorkOrders;
using JXHLJSApp.Services;
using JXHLJSApp.Services.Warehouse;

namespace JXHLJSApp.Pages.Warehouse;

[QueryProperty(nameof(TaskId), "id")]
public partial class PackagingSubTaskDetailPage : ContentPage
{
    private readonly IWarehouseApi _warehouseApi;
    private readonly IScanService _scanService;
    private string? _id;
    private PackagingSubTaskDetailDto? _detail;
    private MaterialQrCodeInfoDto? _scannedMaterial;
    private string? _scannedQrCode;
    private PackagingSubTaskDto? _nextTask;

    public string? TaskId
    {
        get => _id;
        set => _id = Uri.UnescapeDataString(value ?? string.Empty);
    }

    public PackagingSubTaskDetailPage(IWarehouseApi warehouseApi, IScanService scanService)
    {
        InitializeComponent();
        _warehouseApi = warehouseApi;
        _scanService = scanService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDetailAsync();
    }

    private async Task LoadDetailAsync()
    {
        if (string.IsNullOrWhiteSpace(_id))
        {
            await DisplayAlert("提示", "子工序任务ID为空，无法查询详情。", "确定");
            return;
        }

        try
        {
            var detail = await _warehouseApi.GetPackagingSubTaskDetailAsync(_id);
            _detail = detail;
            ResetSaveState();
            ApplyPageMode(detail);
            TaskNoLabel.Text = detail.taskNoDisplay;
            MaterialLabel.Text = $"{detail.materialNameDisplay} {detail.materialCodeDisplay}";
            TemplateLabel.Text = detail.printTemplateNameDisplay;
            PropertyLabel.Text = detail.materialPropertyDisplay;
            MethodLabel.Text = detail.packageMethodDisplay;
            WeightLabel.Text = detail.packageWeightDisplay;
            ClothLabel.Text = detail.needPackagingClothDisplay;
            ColorLabel.Text = detail.packagingClothColorDisplay;
            PalletLabel.Text = detail.needPalletizingDisplay;
            RequirementLabel.Text = detail.otherRequirementDisplay;
            MemoLabel.Text = detail.memoDisplay;
            if (IsPackagedStatus(detail.workOrderStatus))
            {
                ApplyPackagedDetail(detail);
            }
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "加载失败", ex.Message, "确定");
        }
    }


    private async void OnScanClicked(object sender, EventArgs e)
    {
        var code = await _scanService.ScanAsync("包装扫码");
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        try
        {
            var qrCode = code.Trim();
            var material = await _warehouseApi.ScanFinishedPackageQrCodeAsync(qrCode);
            ApplyScannedMaterial(material, qrCode);
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "包装扫码失败", ex.Message, "确定");
        }
    }

    private void ApplyScannedMaterial(MaterialQrCodeInfoDto material, string qrCode)
    {
        ScanSuccessPanel.IsVisible = true;
        ScannedMaterialPanel.IsVisible = true;

        _scannedMaterial = material;
        _scannedQrCode = qrCode;

        ScannedMaterialCodeLabel.Text = $"物料编号：{Display(material.materialCode)}";
        ScannedSteelGradeLabel.Text = Display(material.steelGrade ?? material.materialName);
        ScannedSpecLabel.Text = Display(material.specification ?? material.spec);
        ScannedOriginLabel.Text = Display(material.originPlace);
        ScannedLengthLabel.Text = FormatQuantity(material.length, material.lengthUnit);
        var pieceWeight = material.pieceWeight ?? material.weight;
        var weightUnit = material.weightUnit ?? material.unit ?? "KG";
        ScannedWeightLabel.Text = FormatQuantity(pieceWeight, weightUnit);
        ActualWeightEntry.Text = FormatActualWeightKg(pieceWeight, weightUnit);
    }

    private void ApplyPageMode(PackagingSubTaskDetailDto detail)
    {
        var isPackaged = IsPackagedStatus(detail.workOrderStatus);
        PageTitleLabel.Text = isPackaged ? "包装详情" : "执行包装作业";
        PackagedSummaryPanel.IsVisible = isPackaged;
        PackagedMaterialTitleLabel.IsVisible = isPackaged;
        ScanButton.IsVisible = !isPackaged;
        ScanSuccessPanel.IsVisible = false;
        ScannedMaterialPanel.IsVisible = isPackaged;
        ActualWeightPanel.IsVisible = !isPackaged;
        ActionBar.IsVisible = !isPackaged;
        DetailActualWeightTitleLabel.IsVisible = isPackaged;
        DetailActualWeightLabel.IsVisible = isPackaged;
    }

    private void ApplyPackagedDetail(PackagingSubTaskDetailDto detail)
    {
        PackagedTaskNoLabel.Text = detail.taskNoDisplay;
        PackagedStatusLabel.Text = Display(detail.workOrderStatus);
        PackagedWorkOrderLabel.Text = detail.workOrderNoDisplay;
        PackagedCompleteTimeLabel.Text = detail.completeTimeDisplay;

        ScannedMaterialCodeLabel.Text = $"物料编号：{Display(detail.materialCode)}";
        ScannedSteelGradeLabel.Text = Display(detail.steelGrade ?? detail.materialName);
        ScannedSpecLabel.Text = Display(detail.specification);
        ScannedOriginLabel.Text = Display(detail.originPlace);
        ScannedLengthLabel.Text = FormatQuantity(detail.length, FirstNonEmpty(detail.lengthUnit, "m"));
        var pieceWeight = detail.pieceWeight ?? detail.actualWeight;
        var weightUnit = FirstNonEmpty(detail.weightUnit, detail.unit, "KG");
        ScannedWeightLabel.Text = FormatQuantity(pieceWeight, weightUnit);
        DetailActualWeightLabel.Text = FormatQuantity(detail.actualWeight, "KG");
    }

    private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "--" : value!;

    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string FormatQuantity(decimal? value, string? unit)
    {
        if (!value.HasValue)
        {
            return "--";
        }

        var text = value.Value % 1 == 0 ? value.Value.ToString("0") : value.Value.ToString("0.##");
        return string.IsNullOrWhiteSpace(unit) ? text : $"{text} {unit}";
    }

    private static string FormatActualWeightKg(decimal? value, string? unit)
    {
        if (!value.HasValue)
        {
            return string.Empty;
        }

        var kgValue = IsTonUnit(unit) ? value.Value * 1000 : value.Value;
        return kgValue % 1 == 0 ? kgValue.ToString("0") : kgValue.ToString("0.##");
    }

    private static bool IsTonUnit(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            return false;
        }

        var normalized = unit.Trim();
        return normalized == "吨"
            || string.Equals(normalized, "t", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "ton", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "tons", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPackagedStatus(string? status)
    {
        return !string.IsNullOrWhiteSpace(status)
            && (status.Contains("已完工", StringComparison.OrdinalIgnoreCase)
                || status == "3");
    }


    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (!decimal.TryParse(ActualWeightEntry.Text?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var actualWeight) || actualWeight <= 0)
        {
            await DisplayAlert("提示", "请填写实际重量。", "确定");
            return;
        }

        if (_scannedMaterial is null || string.IsNullOrWhiteSpace(_scannedQrCode))
        {
            await DisplayAlert("提示", "请先完成包装扫码。", "确定");
            return;
        }

        if (_detail is null || string.IsNullOrWhiteSpace(_detail.workOrderNo))
        {
            await DisplayAlert("提示", "生产工单号为空，无法保存包装作业。", "确定");
            return;
        }

        try
        {
            SaveButton.IsEnabled = false;
            var request = new PackagingSaveRequestDto
            {
                actualWeight = actualWeight,
                length = _scannedMaterial.length,
                materialCode = _scannedMaterial.materialCode,
                materialName = _scannedMaterial.materialName,
                originPlace = _scannedMaterial.originPlace,
                pieceWeight = _scannedMaterial.pieceWeight ?? _scannedMaterial.weight,
                qrCode = _scannedQrCode,
                specification = _scannedMaterial.specification ?? _scannedMaterial.spec,
                steelGrade = _scannedMaterial.steelGrade,
                workOrderNo = _detail.workOrderNo
            };

            var saved = await _warehouseApi.SavePackagingAsync(request);
            if (saved != true)
            {
                await ErrorDialogService.ShowAsync(this, "保存失败", "包装作业保存失败，请稍后重试。", "确定");
                return;
            }

            await DisplayAlert("提示", "包装作业保存成功。", "确定");
            await PrepareSavedStateAsync();
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "保存失败", ex.Message, "确定");
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private async Task PrepareSavedStateAsync()
    {
        ScanSuccessPanel.IsVisible = false;
        ScannedMaterialPanel.IsVisible = false;
        ActualWeightEntry.Text = string.Empty;
        _scannedMaterial = null;
        _scannedQrCode = null;
        _nextTask = await FindNextTaskAsync();
        NextTaskButton.IsVisible = _nextTask is not null;
    }

    private async Task<PackagingSubTaskDto?> FindNextTaskAsync()
    {
        var tasks = await _warehouseApi.GetPackagingSubTaskListAsync();
        if (tasks.Count == 0)
        {
            return null;
        }

        var currentIndex = tasks.FindIndex(task => string.Equals(task.id, _id, StringComparison.Ordinal));
        if (currentIndex >= 0)
        {
            return tasks.Skip(currentIndex + 1).FirstOrDefault(task => !string.IsNullOrWhiteSpace(task.id));
        }

        return tasks.FirstOrDefault(task => !string.IsNullOrWhiteSpace(task.id));
    }

    private async void OnNextTaskClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_nextTask?.id))
        {
            return;
        }

        _id = _nextTask.id;
        await LoadDetailAsync();
    }

    private void ResetSaveState()
    {
        ScanSuccessPanel.IsVisible = false;
        ScannedMaterialPanel.IsVisible = false;
        PackagedSummaryPanel.IsVisible = false;
        PackagedMaterialTitleLabel.IsVisible = false;
        ScanButton.IsVisible = true;
        ActualWeightPanel.IsVisible = true;
        ActionBar.IsVisible = true;
        DetailActualWeightTitleLabel.IsVisible = false;
        DetailActualWeightLabel.IsVisible = false;
        NextTaskButton.IsVisible = false;
        ActualWeightEntry.Text = string.Empty;
        _scannedMaterial = null;
        _scannedQrCode = null;
        _nextTask = null;
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
