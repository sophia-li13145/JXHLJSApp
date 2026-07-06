using System.Collections.ObjectModel;
using System.Globalization;
using JXHLJSApp.Models.Warehouse;
using JXHLJSApp.Services;
using JXHLJSApp.Services.Warehouse;
using Microsoft.Maui.ApplicationModel;

namespace JXHLJSApp.Pages.Warehouse;

[QueryProperty(nameof(InstockNo), nameof(InstockNo))]
public partial class AddRawMaterialReceivingPage : ContentPage
{
    private readonly IWarehouseApi _warehouseApi;
    private readonly IScanService _scanService;
    private readonly ObservableCollection<RawMaterialOcrDto> _ocrItems = new();
    private readonly ObservableCollection<MaterialSummaryItem> _summaryItems = new();
    private RawMaterialOcrDto? _pendingOcr;
    private RawMaterialOcrDto? _selectedTicket;
    private string? _instockNo;
    private string? _pendingQrCode;
    private bool _loadedExistingInstock;

    public string? InstockNo
    {
        get => _instockNo;
        set
        {
            _instockNo = Uri.UnescapeDataString(value ?? string.Empty);
            _loadedExistingInstock = false;
        }
    }

    public AddRawMaterialReceivingPage(IWarehouseApi warehouseApi, IScanService scanService)
    {
        InitializeComponent();
        _warehouseApi = warehouseApi;
        _scanService = scanService;
        OcrList.ItemsSource = _ocrItems;
        SummaryList.ItemsSource = _summaryItems;
        var materialTypes = new[] { "原料", "半成品" };
        MaterialTypePicker.ItemsSource = materialTypes;
        BindMaterialTypePicker.ItemsSource = materialTypes;
        MaterialTypePicker.SelectedIndex = 0;
        BindMaterialTypePicker.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (string.IsNullOrWhiteSpace(_instockNo))
        {
            await InitializeBlankInstockAsync();
            return;
        }

        if (!_loadedExistingInstock)
        {
            await InitializeExistingInstockAsync(_instockNo);
        }
    }

    private async Task InitializeExistingInstockAsync(string instockNo)
    {
        try
        {
            InstockNoLabel.Text = instockNo;
            await LoadWarehousesAsync();

            var detail = await _warehouseApi.GetRawMaterialReceivingDetailAsync(instockNo);
            _instockNo = string.IsNullOrWhiteSpace(detail.instockNo) ? instockNo : detail.instockNo;
            InstockNoLabel.Text = _instockNo;
            SelectWarehouse(detail.warehouseDisplay);
            ApplyExistingOcrItems(detail);
            _loadedExistingInstock = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("加载失败", ex.Message, "确定");
        }
    }

    private async Task InitializeBlankInstockAsync()
    {
        try
        {
            var blank = await _warehouseApi.AddBlankInstockAsync();
            _instockNo = blank.instockNo;
            InstockNoLabel.Text = string.IsNullOrWhiteSpace(_instockNo) ? "--" : _instockNo;

            await LoadWarehousesAsync();
            _loadedExistingInstock = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("初始化失败", ex.Message, "确定");
        }
    }

    private async Task LoadWarehousesAsync()
    {
        var warehouses = await _warehouseApi.QueryWarehouseInfoAsync();
        WarehousePicker.ItemsSource = warehouses;
        if (warehouses.Count > 0 && WarehousePicker.SelectedIndex < 0)
        {
            WarehousePicker.SelectedIndex = 0;
        }
    }

    private void SelectWarehouse(string? warehouseName)
    {
        if (WarehousePicker.ItemsSource is not IEnumerable<WarehouseInfoDto> warehouses || string.IsNullOrWhiteSpace(warehouseName))
        {
            return;
        }

        var index = warehouses
            .Select((warehouse, itemIndex) => new { warehouse, itemIndex })
            .FirstOrDefault(item => string.Equals(item.warehouse.displayName, warehouseName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.warehouse.warehouseName, warehouseName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.warehouse.warehouseCode, warehouseName, StringComparison.OrdinalIgnoreCase))?
            .itemIndex;

        if (index.HasValue)
        {
            WarehousePicker.SelectedIndex = index.Value;
        }
    }

    private void ApplyExistingOcrItems(RawMaterialReceivingDetailDto detail)
    {
        _ocrItems.Clear();
        foreach (var item in detail.ocrItemsForEdit)
        {
            _ocrItems.Add(item);
        }

        MaterialListTitle.Text = $"待入库列表 ({_ocrItems.Count})";
        ClearSelectedTicket();
    }

    private async void OnTakePhotoClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_instockNo))
        {
            await DisplayAlert("提示", "入库单号尚未生成，请稍后重试。", "确定");
            return;
        }

        try
        {
            var photo = await GetTicketPhotoAsync();
            if (photo is null) return;

            ExtractedTextLabel.Text = "图片上传与识别中...";
            var attachment = await _warehouseApi.UploadAttachmentAsync(photo, "toolingManager", "images");
            var ocr = await _warehouseApi.RecognizeIncomingAsync(attachment, _instockNo);
            ocr.attachmentName = attachment.attachmentName ?? attachment.attachmentRealName;
            ocr.attachmentUrl = attachment.attachmentUrl;
            ShowTicketConfirmDialog(ocr);
        }
        catch (Exception ex)
        {
            ExtractedTextLabel.Text = "暂无提取的票签内容";
            await DisplayAlert("识别失败", ex.Message, "确定");
        }
    }

    private async Task<FileResult?> GetTicketPhotoAsync()
    {
        var captureSupported = MediaPicker.Default.IsCaptureSupported;
        var choice = captureSupported
            ? await DisplayActionSheet("上传票签图片", "取消", null, "拍照", "从相册选择")
            : await DisplayActionSheet("当前设备不支持直接拍照，可从相册选择票签图片", "取消", null, "从相册选择");

        if (choice == "取消" || string.IsNullOrWhiteSpace(choice)) return null;

        if (choice == "从相册选择")
        {
            return await PickTicketPhotoAsync();
        }

        var permission = await Permissions.RequestAsync<Permissions.Camera>();
        if (permission != PermissionStatus.Granted)
        {
            var fallback = await DisplayActionSheet("未授予摄像头权限，可从相册选择票签图片", "取消", null, "从相册选择");
            return fallback == "从相册选择" ? await PickTicketPhotoAsync() : null;
        }

        try
        {
            return await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions { Title = "拍摄票签" });
        }
        catch (FeatureNotSupportedException)
        {
            return await PickTicketPhotoAsync();
        }
    }

    private static async Task<FileResult?> PickTicketPhotoAsync()
    {
        return await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions { Title = "选择票签图片" });
    }

    private async void OnCalculateSummaryClicked(object sender, EventArgs e)
    {
        _summaryItems.Clear();
        var summaries = _ocrItems
            .GroupBy(item => string.IsNullOrWhiteSpace(item.materialName) ? "--" : item.materialName!.Trim())
            .Select(group =>
            {
                var first = group.First();
                var total = group.Sum(item => ParseWeight(item.pieceWeight));
                return new MaterialSummaryItem(
                    group.Key,
                    string.IsNullOrWhiteSpace(first.materialType) ? "原料" : first.materialType!.Trim(),
                    string.IsNullOrWhiteSpace(first.originPlace) ? "--" : first.originPlace!.Trim(),
                    group.Count(),
                    total);
            })
            .OrderBy(item => item.materialName)
            .ToList();

        foreach (var item in summaries)
        {
            _summaryItems.Add(item);
        }

        if (_summaryItems.Count == 0)
        {
            await DisplayAlert("提示", "暂无待入库物料可汇总。", "确定");
            return;
        }

        SummaryOverlay.IsVisible = true;
    }

    private static decimal ParseWeight(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0m;

        var isKg = value.Contains("KG", StringComparison.OrdinalIgnoreCase);
        var text = value.Trim().Replace("吨", string.Empty).Replace("KG", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        var parsed = decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var invariantValue)
            ? invariantValue
            : decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out var currentValue) ? currentValue : 0m;
        return isKg ? parsed / 1000m : parsed;
    }

    private void OnCloseSummaryTapped(object sender, TappedEventArgs e) => SummaryOverlay.IsVisible = false;

    private void OnCloseSummaryClicked(object sender, EventArgs e) => SummaryOverlay.IsVisible = false;

    private async void OnScanBindClicked(object sender, EventArgs e)
    {
        if (_selectedTicket is null)
        {
            await DisplayAlert("提示", "请先拍照识别并确认票签内容。", "确定");
            return;
        }

        try
        {
            var qsCode = await _scanService.ScanAsync("扫码绑定");
            if (string.IsNullOrWhiteSpace(qsCode)) return;

            var qrInfo = await _warehouseApi.QueryQrCodeInfoAsync(qsCode.Trim());
            ShowBindConfirmDialog(qrInfo.qrCode ?? qsCode.Trim(), _selectedTicket);
        }
        catch (Exception ex)
        {
            await DisplayAlert("扫码绑定失败", ex.Message, "确定");
        }
    }

    private void ShowBindConfirmDialog(string qrCode, RawMaterialOcrDto source)
    {
        _pendingQrCode = qrCode;
        BindQrCodeEntry.Text = qrCode;
        BindMaterialTypePicker.SelectedItem = string.IsNullOrWhiteSpace(source.materialType) ? "原料" : source.materialType;
        BindMaterialNameEntry.Text = source.materialName;
        BindSpecEntry.Text = source.spec;
        BindFurnaceNoEntry.Text = source.furnaceNo;
        BindOriginPlaceEntry.Text = source.originPlace;
        BindPieceWeightEntry.Text = source.pieceWeight;
        BindConfirmOverlay.IsVisible = true;
    }

    private void OnConfirmBindClicked(object sender, EventArgs e)
    {
        var materialType = BindMaterialTypePicker.SelectedItem?.ToString();
        var bound = new RawMaterialOcrDto
        {
            qrCode = _pendingQrCode,
            materialType = string.IsNullOrWhiteSpace(materialType) ? _selectedTicket?.materialType : materialType,
            materialName = BindMaterialNameEntry.Text,
            spec = BindSpecEntry.Text,
            furnaceNo = BindFurnaceNoEntry.Text,
            originPlace = BindOriginPlaceEntry.Text,
            pieceWeight = BindPieceWeightEntry.Text,
            pieceWeightUnit = "吨",
            coilCount = _selectedTicket?.coilCount,
            coilDiameter = _selectedTicket?.coilDiameter,
            ocrRawText = _selectedTicket?.ocrRawText,
            strength = _selectedTicket?.strength,
            attachmentName = _selectedTicket?.attachmentName,
            attachmentUrl = _selectedTicket?.attachmentUrl
        };

        _ocrItems.Add(bound);
        MaterialListTitle.Text = $"待入库列表 ({_ocrItems.Count})";
        BindConfirmOverlay.IsVisible = false;
    }

    private void OnCloseBindConfirmTapped(object sender, TappedEventArgs e) => BindConfirmOverlay.IsVisible = false;

    private void OnCancelBindConfirmClicked(object sender, EventArgs e) => BindConfirmOverlay.IsVisible = false;

    private void ShowTicketConfirmDialog(RawMaterialOcrDto ocr)
    {
        _pendingOcr = ocr;
        MaterialTypePicker.SelectedItem = string.IsNullOrWhiteSpace(ocr.materialType) ? "原料" : ocr.materialType;
        MaterialNameEntry.Text = ocr.materialName;
        SpecEntry.Text = ocr.spec;
        FurnaceNoEntry.Text = ocr.furnaceNo;
        OriginPlaceEntry.Text = ocr.originPlace;
        PieceWeightEntry.Text = ocr.pieceWeight;
        TicketConfirmOverlay.IsVisible = true;
    }

    private void OnConfirmTicketClicked(object sender, EventArgs e)
    {
        var materialType = MaterialTypePicker.SelectedItem?.ToString();
        _selectedTicket = new RawMaterialOcrDto
        {
            materialType = string.IsNullOrWhiteSpace(materialType) ? _pendingOcr?.materialType : materialType,
            materialName = MaterialNameEntry.Text,
            spec = SpecEntry.Text,
            furnaceNo = FurnaceNoEntry.Text,
            originPlace = OriginPlaceEntry.Text,
            pieceWeight = PieceWeightEntry.Text,
            pieceWeightUnit = "吨",
            coilCount = _pendingOcr?.coilCount,
            coilDiameter = _pendingOcr?.coilDiameter,
            ocrRawText = _pendingOcr?.ocrRawText,
            strength = _pendingOcr?.strength,
            attachmentName = _pendingOcr?.attachmentName,
            attachmentUrl = _pendingOcr?.attachmentUrl
        };

        ApplySelectedTicket(_selectedTicket);
        TicketConfirmOverlay.IsVisible = false;
    }

    private void ApplySelectedTicket(RawMaterialOcrDto ticket)
    {
        ExtractedTextLabel.IsVisible = false;
        SelectedTicketCard.IsVisible = true;
        SelectedMaterialTypeLabel.Text = string.IsNullOrWhiteSpace(ticket.materialType) ? "原料" : ticket.materialType;
        SelectedMaterialNameLabel.Text = ticket.materialNameDisplay;
        SelectedSpecLabel.Text = ticket.specDisplay;
        SelectedFurnaceNoLabel.Text = ticket.furnaceNoDisplay;
        SelectedOriginPlaceLabel.Text = ticket.originPlaceDisplay;
        SelectedPieceWeightLabel.Text = ticket.pieceWeightDisplay;
    }

    private void ClearSelectedTicket()
    {
        _selectedTicket = null;
        SelectedTicketCard.IsVisible = false;
        ExtractedTextLabel.IsVisible = true;
        ExtractedTextLabel.Text = "暂无提取的票签内容";
    }

    private async void OnPreviewSelectedTicketTapped(object sender, TappedEventArgs e)
    {
        if (_selectedTicket is null)
        {
            return;
        }

        await PreviewAttachmentAsync(_selectedTicket.attachmentUrl);
    }

    private async void OnPreviewOcrItemTapped(object sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is RawMaterialOcrDto item)
        {
            await PreviewAttachmentAsync(item.attachmentUrl);
        }
    }

    private async Task PreviewAttachmentAsync(string? attachmentUrl)
    {
        if (string.IsNullOrWhiteSpace(attachmentUrl))
        {
            await DisplayAlert("提示", "当前票签暂无可预览图片。", "确定");
            return;
        }

        try
        {
            var previewUrl = await _warehouseApi.PreviewAttachmentAsync(attachmentUrl);
            if (string.IsNullOrWhiteSpace(previewUrl))
            {
                await DisplayAlert("提示", "附件预览地址为空。", "确定");
                return;
            }

            await Browser.Default.OpenAsync(previewUrl, BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception ex)
        {
            await DisplayAlert("预览失败", ex.Message, "确定");
        }
    }

    private void OnDeleteTicketTapped(object sender, TappedEventArgs e) => ClearSelectedTicket();

    private void OnCloseTicketConfirmTapped(object sender, TappedEventArgs e) => TicketConfirmOverlay.IsVisible = false;

    private void OnCancelTicketConfirmClicked(object sender, EventArgs e) => TicketConfirmOverlay.IsVisible = false;

    private async void OnBackLabelTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnCancelClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}

internal sealed record MaterialSummaryItem(string materialName, string materialType, string originPlace, int count, decimal totalWeight)
{
    public string totalWeightDisplay => $"{totalWeight:0.00} 吨";
}
