using System.Globalization;
using JXHLJSApp.Models.Warehouse;
using JXHLJSApp.Models.WorkOrders;
using JXHLJSApp.Services;
using JXHLJSApp.Services.Warehouse;

namespace JXHLJSApp.Pages.Warehouse;

[QueryProperty(nameof(TaskId), "id")]
public partial class PackagingSubTaskDetailPage : ContentPage, IQueryAttributable
{
    private readonly IWarehouseApi _warehouseApi;
    private readonly IScanService _scanService;
    private string? _id;
    private PackagingSubTaskDetailDto? _detail;
    private MaterialQrCodeInfoDto? _scannedMaterial;
    private string? _scannedQrCode;
    private PackagingSubTaskDto? _nextTask;
    private string? _loadedTaskId;
    private bool _isQuickPackaging;
    private string? _quickQrCode;
    private bool _quickLoadAttempted;
    private bool _isLoadingQuickDetail;
    private bool _quickScanRequested;
    private bool _quickScanStarted;
    private bool _isStartingQuickScan;
    private bool _isSaving;

    private bool CanEditPackaging => _scannedMaterial?.isEditable == true
        && _detail is not null && !IsReadOnlyDetail(_detail);

    private void UpdateEditingState()
    {
        ActualWeightEntry.IsEnabled = CanEditPackaging && !_isSaving;
        SaveButton.IsEnabled = CanEditPackaging && !_isSaving;
    }

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

        // Closing a modal error dialog reactivates the underlying page on Android.
        // Do not treat that lifecycle callback as a new backend operation.
        if (ErrorDialogService.IsDialogVisible)
        {
            return;
        }
        if (_isStartingQuickScan) return;
        if (_quickScanRequested)
        {
            _quickScanRequested = false;
            await StartQuickScanAsync();
            return;
        }
        if (!string.IsNullOrWhiteSpace(_quickQrCode))
        {
            if (!_quickLoadAttempted)
                await LoadQuickDetailAsync();
            return;
        }
        if (_detail is not null && string.Equals(_loadedTaskId, _id, StringComparison.Ordinal))
        {
            return;
        }
        if (_isQuickPackaging && _quickScanStarted) return;

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
            _isQuickPackaging = false;
            ApplyDetail(detail);
            _loadedTaskId = _id;
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "加载失败", ex.Message, "确定");
        }
    }


    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("startPackagingScan", out var startScan) && startScan is true)
        {
            if (!_quickScanStarted)
            {
                _isQuickPackaging = true;
                _quickScanRequested = true;
                ShowQuickLoading();
            }
            return;
        }
        if (query.TryGetValue("id", out var id))
        {
            _id = id?.ToString();
        }
        if (!query.ContainsKey("scannedPackagingDetail")
            && query.TryGetValue("scannedPackagingQrCode", out var pendingCode)
            && pendingCode is string pendingQrCode && !string.IsNullOrWhiteSpace(pendingQrCode))
        {
            _isQuickPackaging = true;
            _quickQrCode = pendingQrCode;
            _quickLoadAttempted = false;
            ShowQuickLoading();
            return;
        }
        if (!query.TryGetValue("scannedPackagingDetail", out var value)
            || value is not PackagingSubTaskDetailDto detail
            || !query.TryGetValue("scannedPackagingQrCode", out var code)
            || code is not string qrCode || string.IsNullOrWhiteSpace(qrCode))
        {
            return;
        }

        _isQuickPackaging = true;
        _id = detail.id;
        ApplyDetail(detail);
        if (!IsReadOnlyDetail(detail))
        {
            ApplyScannedMaterial(new MaterialQrCodeInfoDto
            {
                isEditable = detail.isEditable,
                length = detail.length,
                materialCode = detail.materialCode,
                materialName = detail.materialName,
                originPlace = detail.originPlace,
                pieceWeight = detail.pieceWeight,
                specification = detail.specification,
                steelGrade = detail.steelGrade,
                workOrderNo = detail.workOrderNo
            }, qrCode);
        }
        _loadedTaskId = _id;
    }

    private void ShowQuickLoading()
    {
        DetailScrollView.IsVisible = false;
        ActionBar.IsVisible = false;
        LoadingPanel.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        LoadingLabel.Text = "正在加载包装详情…";
        RetryLoadButton.IsVisible = false;
    }

    private async Task LoadQuickDetailAsync()
    {
        if (_isLoadingQuickDetail || string.IsNullOrWhiteSpace(_quickQrCode)) return;
        _isLoadingQuickDetail = true;
        _quickLoadAttempted = true;
        ShowQuickLoading();
        try
        {
            var qrCode = _quickQrCode;
            var detail = await _warehouseApi.ScanPackagingDetailAsync(qrCode);
            ApplyQueryAttributes(new Dictionary<string, object>
            {
                ["scannedPackagingDetail"] = detail,
                ["scannedPackagingQrCode"] = qrCode
            });
            _quickQrCode = null;
            LoadingPanel.IsVisible = false;
            DetailScrollView.IsVisible = true;
        }
        catch (Exception ex)
        {
            LoadingLabel.Text = "包装详情加载失败，请重试。";
            RetryLoadButton.IsVisible = true;
            await ErrorDialogService.ShowAsync(this, "包装详情加载失败", ex.Message, "确定");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            _isLoadingQuickDetail = false;
        }
    }

    private async Task StartQuickScanAsync()
    {
        if (_isStartingQuickScan) return;
        _quickScanStarted = true;
        _isStartingQuickScan = true;
        ShowQuickLoading();
        try
        {
            // Let the initial detail-page navigation finish before pushing a modal.
            await Task.Yield();
            var code = await _scanService.ScanAsync("扫码包装", formats: ZXing.Net.Maui.BarcodeFormat.QrCode);
            if (string.IsNullOrWhiteSpace(code))
            {
                await Shell.Current.GoToAsync("..", false);
                return;
            }

            _quickQrCode = code.Trim();
            await LoadQuickDetailAsync();
        }
        catch (Exception ex)
        {
            LoadingLabel.Text = "扫码未完成，请重试。";
            RetryLoadButton.IsVisible = true;
            await ErrorDialogService.ShowAsync(this, "包装扫码失败", ex.Message, "确定");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            _isStartingQuickScan = false;
        }
    }

    private async void OnRetryLoadClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_quickQrCode))
            await StartQuickScanAsync();
        else
            await LoadQuickDetailAsync();
    }

    private void ApplyDetail(PackagingSubTaskDetailDto detail)
    {
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
        if (IsReadOnlyDetail(detail))
        {
            ApplyPackagedDetail(detail);
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
            if (_isQuickPackaging)
            {
                var detail = await _warehouseApi.ScanPackagingDetailAsync(qrCode);
                ApplyQueryAttributes(new Dictionary<string, object>
                {
                    ["scannedPackagingDetail"] = detail,
                    ["scannedPackagingQrCode"] = qrCode
                });
                return;
            }
            var material = await _warehouseApi.ScanFinishedPackageQrCodeAsync(qrCode);
            if (material.isEditable == false && !string.IsNullOrWhiteSpace(_id))
            {
                var detail = await _warehouseApi.GetPackagingSubTaskDetailAsync(_id);
                detail.isEditable = false;
                ApplyDetail(detail);
                return;
            }
            if (_detail is not null)
            {
                _detail.isEditable = material.isEditable;
                ApplyPageMode(_detail);
            }
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
        ScannedLengthLabel.Text = FormatQuantity(material.length, FirstNonEmpty(material.lengthUnit, "m"));
        var pieceWeight = material.pieceWeight ?? material.weight;
        ScannedWeightLabel.Text = FormatQuantityWithoutUnitSpace(pieceWeight, "KG");
        ActualWeightEntry.Text = string.Empty;
        UpdateEditingState();
    }

    private void ApplyPageMode(PackagingSubTaskDetailDto detail)
    {
        var isPackaged = IsReadOnlyDetail(detail);
        PageTitleLabel.Text = isPackaged ? "包装详情" : "执行包装作业";
        InstructionTitleLabel.Text = isPackaged ? "包装明细" : "包装指令明细";
        Grid.SetColumnSpan(TemplateLabel, isPackaged ? 1 : 3);
        TaskNoTitleLabel.IsVisible = !isPackaged;
        TaskNoLabel.IsVisible = !isPackaged;
        MaterialTitleLabel.IsVisible = !isPackaged;
        MaterialLabel.IsVisible = !isPackaged;
        PackagedSummaryPanel.IsVisible = isPackaged;
        PackagedMaterialsPanel.IsVisible = isPackaged;
        ScanButton.IsVisible = !isPackaged && !_isQuickPackaging;
        ScanSuccessPanel.IsVisible = false;
        ScannedMaterialPanel.IsVisible = false;
        ActualWeightPanel.IsVisible = !isPackaged;
        ActionBar.IsVisible = !isPackaged;
        DetailActualWeightTitleLabel.IsVisible = isPackaged;
        DetailActualWeightLabel.IsVisible = isPackaged;
    }

    private void ApplyPackagedDetail(PackagingSubTaskDetailDto detail)
    {
        var packagedMaterials = detail.packagedMaterialList?.ToList() ?? new List<PackagedMaterialDto>();
        if (packagedMaterials.Count == 0)
        {
            packagedMaterials.Add(new PackagedMaterialDto
            {
                materialCode = detail.materialCode,
                steelGrade = FirstNonEmpty(detail.steelGrade, detail.materialName),
                specification = detail.specification,
                originPlace = detail.originPlace,
                length = detail.length,
                lengthUnit = detail.lengthUnit,
                pieceWeight = detail.pieceWeight ?? detail.actualWeight
            });
        }

        var packagedWeights = packagedMaterials
            .Where(item => item.pieceWeight.HasValue)
            .Select(item => item.pieceWeight!.Value)
            .ToList();
        var actualWeight = detail.actualWeight
            ?? (packagedWeights?.Count > 0 ? packagedWeights.Sum() : null);

        PackagedTaskNoLabel.Text = detail.taskNoDisplay;
        PackagedStatusLabel.Text = Display(detail.workOrderStatus);
        PackagedWorkOrderLabel.Text = detail.workOrderNoDisplay;
        PackagedCompleteTimeLabel.Text = detail.completeTimeDisplay;

        BindableLayout.SetItemsSource(PackagedMaterialsLayout, packagedMaterials);
        DetailActualWeightLabel.Text = FormatQuantity(actualWeight, "KG");
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

    private static string FormatQuantityWithoutUnitSpace(decimal? value, string? unit)
    {
        if (!value.HasValue)
        {
            return "--";
        }

        var text = value.Value % 1 == 0 ? value.Value.ToString("0") : value.Value.ToString("0.##");
        return string.IsNullOrWhiteSpace(unit) ? text : $"{text}{unit}";
    }

    private static bool IsReadOnlyDetail(PackagingSubTaskDetailDto detail)
    {
        return detail.isEditable.HasValue
            ? !detail.isEditable.Value
            : PackagingSubTaskDetailDto.IsPackagedStatus(detail.workOrderStatus);
    }


    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (!CanEditPackaging || _isSaving) return;

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
            _isSaving = true;
            UpdateEditingState();
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

            try
            {
                await _warehouseApi.PrintPackagingLabelAsync(_detail.workOrderNo);
                await DisplayAlert("提示", "包装作业保存并提交打印成功。", "确定");
            }
            catch (Exception ex)
            {
                await ErrorDialogService.ShowAsync(this, "打印失败", $"包装作业已保存，但打印失败：{ex.Message}", "确定");
            }
            await PrepareSavedStateAsync();
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "保存失败", ex.Message, "确定");
        }
        finally
        {
            _isSaving = false;
            UpdateEditingState();
        }
    }

    private async Task PrepareSavedStateAsync()
    {
        ScanSuccessPanel.IsVisible = false;
        ScannedMaterialPanel.IsVisible = false;
        ActualWeightEntry.Text = string.Empty;
        _scannedMaterial = null;
        _scannedQrCode = null;
        UpdateEditingState();
        if (_isQuickPackaging)
        {
            // The scanned material has been consumed by the successful save.
            // Refresh the saved record rather than leaving an unusable input form.
            ActualWeightPanel.IsVisible = false;
            ActionBar.IsVisible = false;
            try
            {
                var detail = await _warehouseApi.GetPackagingSubTaskDetailAsync(_id!);
                ApplyDetail(detail);
                _loadedTaskId = _id;
                if (!IsReadOnlyDetail(detail))
                {
                    // Do not allow another save using the already consumed scan,
                    // even if the backend has not yet updated the task status.
                    ActualWeightPanel.IsVisible = false;
                    ActionBar.IsVisible = false;
                    await DisplayAlert("提示", "包装作业已保存，请返回首页重新扫码查看最新状态。", "确定");
                }
            }
            catch (Exception ex)
            {
                await ErrorDialogService.ShowAsync(this, "详情刷新失败", $"包装作业已保存，但详情刷新失败：{ex.Message}", "确定");
            }
            return;
        }
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
        PackagedMaterialsPanel.IsVisible = false;
        BindableLayout.SetItemsSource(PackagedMaterialsLayout, null);
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
        UpdateEditingState();
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}


