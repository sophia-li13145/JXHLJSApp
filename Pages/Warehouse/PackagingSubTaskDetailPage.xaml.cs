using System.Globalization;
using JXHLJSApp.Models.Warehouse;
using JXHLJSApp.Models.WorkOrders;
using JXHLJSApp.Services;
using JXHLJSApp.Services.Warehouse;
using JXHLJSApp.Services.WorkOrders;

namespace JXHLJSApp.Pages.Warehouse;

[QueryProperty(nameof(TaskId), "id")]
public partial class PackagingSubTaskDetailPage : ContentPage
{
    private readonly IWarehouseApi _warehouseApi;
    private readonly IScanService _scanService;
    private readonly IWorkOrderApi _workOrderApi;
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

    public PackagingSubTaskDetailPage(IWarehouseApi warehouseApi, IScanService scanService, IWorkOrderApi workOrderApi)
    {
        InitializeComponent();
        _warehouseApi = warehouseApi;
        _scanService = scanService;
        _workOrderApi = workOrderApi;
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
        }
        catch (Exception ex)
        {
            await DisplayAlert("加载失败", ex.Message, "确定");
        }
    }


    private async void OnTemplateTapped(object sender, TappedEventArgs e)
    {
        var template = _detail?.printTemplate;
        if (template is null || string.IsNullOrWhiteSpace(template.attachmentUrl))
        {
            await DisplayAlert("提示", "当前包装任务暂无可预览的打印模板。", "确定");
            return;
        }

        try
        {
            var preview = await _warehouseApi.PreviewAttachmentAsync(template.attachmentUrl);
            if (string.IsNullOrWhiteSpace(preview))
            {
                await DisplayAlert("提示", "打印模板预览内容为空。", "确定");
                return;
            }

            if (ShouldOpenWithExternalViewer(template, preview))
            {
                await Launcher.OpenAsync(new Uri(preview));
                return;
            }

            await Navigation.PushModalAsync(CreatePreviewPage(template, preview));
        }
        catch (Exception ex)
        {
            await DisplayAlert("预览失败", ex.Message, "确定");
        }
    }

    private static bool ShouldOpenWithExternalViewer(AttachmentDto template, string preview)
    {
        if (!Uri.TryCreate(preview, UriKind.Absolute, out _))
        {
            return false;
        }

        var fileName = FirstNonEmpty(template.attachmentName, template.attachmentRealName, template.attachmentUrl, preview);
        var extension = Path.GetExtension(fileName)?.TrimStart('.');
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = template.attachmentExt;
        }

        return IsOfficeDocumentExtension(extension);
    }

    private static bool IsOfficeDocumentExtension(string? extension)
    {
        return extension?.Trim().TrimStart('.').ToLowerInvariant() switch
        {
            "xls" or "xlsx" or "xlsm" or "csv" or "doc" or "docx" or "ppt" or "pptx" => true,
            _ => false
        };
    }

    private static ContentPage CreatePreviewPage(AttachmentDto template, string preview)
    {
        var title = FirstNonEmpty(template.attachmentName, template.attachmentRealName) ?? "打印模板预览";
        var closeButton = new Button
        {
            Text = "关闭",
            BackgroundColor = Color.FromArgb("#1F447E"),
            TextColor = Colors.White,
            CornerRadius = 10,
            HeightRequest = 44
        };

        var titleLabel = new Label
        {
            Text = title,
            TextColor = Color.FromArgb("#0A2E69"),
            FontAttributes = FontAttributes.Bold,
            FontSize = 18,
            Margin = new Thickness(18, 18, 18, 10)
        };
        var previewSource = Uri.TryCreate(preview, UriKind.Absolute, out _)
            ? new UrlWebViewSource { Url = preview }
            : (WebViewSource)new HtmlWebViewSource { Html = preview };
        var previewWebView = new WebView
        {
            Source = previewSource,
            Margin = new Thickness(12, 0)
        };
        closeButton.Margin = new Thickness(18, 12, 18, 18);

        var layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };
        layout.Add(titleLabel, 0, 0);
        layout.Add(previewWebView, 0, 1);
        layout.Add(closeButton, 0, 2);

        var page = new ContentPage
        {
            Title = title,
            BackgroundColor = Colors.White,
            Content = layout
        };

        closeButton.Clicked += async (_, _) => await page.Navigation.PopModalAsync();
        return page;
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
            var material = await _workOrderApi.ScanQueryMaterialInfoAsync(qrCode);
            ApplyScannedMaterial(material, qrCode);
        }
        catch (Exception ex)
        {
            await DisplayAlert("包装扫码失败", ex.Message, "确定");
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
        ScannedWeightLabel.Text = FormatQuantity(material.weight, material.weightUnit ?? material.unit ?? "KG");
        ActualWeightEntry.Text = material.weight?.ToString("0.##") ?? string.Empty;
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
                pieceWeight = _scannedMaterial.weight,
                qrCode = _scannedQrCode,
                specification = _scannedMaterial.specification ?? _scannedMaterial.spec,
                steelGrade = _scannedMaterial.steelGrade,
                workOrderNo = _detail.workOrderNo
            };

            var saved = await _warehouseApi.SavePackagingAsync(request);
            if (saved != true)
            {
                await DisplayAlert("保存失败", "包装作业保存失败，请稍后重试。", "确定");
                return;
            }

            await DisplayAlert("提示", "包装作业保存成功。", "确定");
            await PrepareSavedStateAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("保存失败", ex.Message, "确定");
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
        NextTaskButton.IsVisible = false;
        ActualWeightEntry.Text = string.Empty;
        _scannedMaterial = null;
        _scannedQrCode = null;
        _nextTask = null;
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
