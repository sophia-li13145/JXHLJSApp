using System.Collections.ObjectModel;
using System.Globalization;
using JXHLJSApp.Models.Warehouse;
using JXHLJSApp.Services;
using JXHLJSApp.Services.Warehouse;

namespace JXHLJSApp.Pages.Warehouse;

public partial class AddRawMaterialReceivingPage : ContentPage, IQueryAttributable
{
    private readonly IWarehouseApi _warehouseApi;
    private readonly IScanService _scanService;
    private readonly ObservableCollection<RawMaterialOcrDto> _ocrItems = new();
    private readonly ObservableCollection<RawMaterialOcrDto> _ticketItems = new();
    private readonly ObservableCollection<MaterialSummaryItem> _summaryItems = new();
    private RawMaterialOcrDto? _pendingOcr;
    private RawMaterialOcrDto? _selectedTicket;
    private AttachmentDto? _pendingTicketAttachment;
    private List<WarehouseInfoDto> _warehouses = new();
    private List<DictItemDto> _materialClassOptions = CreateDefaultMaterialClassOptions();
    private string? _instockNo;
    private string? _pendingQrCode;
    private bool _isExistingInstock;
    private bool _loadedExistingInstock;

    public AddRawMaterialReceivingPage(IWarehouseApi warehouseApi, IScanService scanService)
    {
        InitializeComponent();
        _warehouseApi = warehouseApi;
        _scanService = scanService;
        OcrList.ItemsSource = _ocrItems;
        TicketList.ItemsSource = _ticketItems;
        SummaryList.ItemsSource = _summaryItems;
        ApplyMaterialClassOptions(_materialClassOptions);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("InstockNo", out var value))
        {
            _instockNo = Uri.UnescapeDataString(value?.ToString() ?? string.Empty);
            _isExistingInstock = !string.IsNullOrWhiteSpace(_instockNo);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_isExistingInstock && !string.IsNullOrWhiteSpace(_instockNo))
        {
            if (!_loadedExistingInstock)
            {
                await LoadExistingInstockAsync(_instockNo);
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(_instockNo))
        {
            await InitializeBlankInstockAsync();
        }
    }

    private async Task InitializeBlankInstockAsync()
    {
        try
        {
            var blank = await _warehouseApi.AddBlankInstockAsync();
            _instockNo = blank.instockNo;
            InstockNoLabel.Text = string.IsNullOrWhiteSpace(_instockNo) ? "--" : _instockNo;

            await LoadMaterialClassOptionsAsync();
            await LoadWarehousesAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("初始化失败", ex.Message, "确定");
        }
    }


    private async Task LoadExistingInstockAsync(string instockNo)
    {
        try
        {
            PageTitleLabel.Text = "新增采购入库";
            InstockNoLabel.Text = instockNo;
            await LoadMaterialClassOptionsAsync();
            await LoadWarehousesAsync();

            var detail = await _warehouseApi.GetRawMaterialReceivingDetailAsync(instockNo);
            _instockNo = detail.instockNo ?? instockNo;
            InstockNoLabel.Text = string.IsNullOrWhiteSpace(_instockNo) ? "--" : _instockNo;
            SelectWarehouse(detail.detailItems.FirstOrDefault());
            ApplyOcrList(detail.ocrList);
            ApplyDetailList(detail.detailItems);
            _loadedExistingInstock = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("加载失败", ex.Message, "确定");
        }
    }

    private async Task LoadMaterialClassOptionsAsync()
    {
        try
        {
            var dictGroups = await _warehouseApi.GetRawMaterialReceivingDictListAsync();
            var options = dictGroups
                .FirstOrDefault(group => IsMaterialClassField(group.field))?
                .dictItems?
                .Where(IsSupportedMaterialClassOption)
                .ToList();

            if (options?.Count > 0)
            {
                _materialClassOptions = options;
                ApplyMaterialClassOptions(_materialClassOptions);
            }
            else
            {
                _materialClassOptions = CreateDefaultMaterialClassOptions();
                ApplyMaterialClassOptions(_materialClassOptions);
            }
        }
        catch
        {
            ApplyMaterialClassOptions(_materialClassOptions);
        }
    }

    private static bool IsMaterialClassField(string? field)
    {
        if (string.IsNullOrWhiteSpace(field)) return false;

        var normalized = field.Replace("_", string.Empty).Replace("-", string.Empty);
        return string.Equals(normalized, "materialClass", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "materialType", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSupportedMaterialClassOption(DictItemDto item)
    {
        var value = FirstNonEmpty(item.dictItemName, item.dictItemValue);
        return value.Contains("原料", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("raw", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("半成品", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("semi", StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyMaterialClassOptions(List<DictItemDto> options)
    {
        MaterialTypePicker.ItemsSource = options;
        BindMaterialTypePicker.ItemsSource = options;
        if (options.Count == 0) return;

        if (MaterialTypePicker.SelectedItem is not DictItemDto materialType || !options.Contains(materialType))
        {
            MaterialTypePicker.SelectedIndex = 0;
            MaterialTypePicker.SelectedItem = options[0];
        }

        if (BindMaterialTypePicker.SelectedItem is not DictItemDto bindMaterialType || !options.Contains(bindMaterialType))
        {
            BindMaterialTypePicker.SelectedIndex = 0;
            BindMaterialTypePicker.SelectedItem = options[0];
        }
    }

    private async Task LoadWarehousesAsync()
    {
        _warehouses = await _warehouseApi.QueryWarehouseInfoAsync();
        WarehousePicker.ItemsSource = _warehouses;
        if (_warehouses.Count > 0 && WarehousePicker.SelectedItem is not WarehouseInfoDto)
        {
            WarehousePicker.SelectedIndex = 0;
            WarehousePicker.SelectedItem = _warehouses[0];
        }
    }

    private void SelectWarehouse(RawMaterialReceivingDetailItemDto? firstDetail)
    {
        if (firstDetail is null || _warehouses.Count == 0) return;

        var index = _warehouses.FindIndex(warehouse =>
            string.Equals(warehouse.selectedCode, firstDetail.instockWarehouseCode, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(warehouse.selectedName, firstDetail.instockWarehouse, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            WarehousePicker.SelectedIndex = index;
            WarehousePicker.SelectedItem = _warehouses[index];
        }
    }

    private void ApplyOcrList(IEnumerable<RawMaterialReceivingOcrDto>? ocrList)
    {
        _ticketItems.Clear();

        foreach (var ocr in ocrList ?? Enumerable.Empty<RawMaterialReceivingOcrDto>())
        {
            _ticketItems.Add(new RawMaterialOcrDto
            {
                coilCount = FormatDecimal(ocr.coilCount),
                coilDiameter = FormatDecimal(ocr.coilDiameter),
                furnaceNo = ocr.furnaceNo,
                materialClass = ocr.materialClass,
                materialClassName = ResolveMaterialClassName(ocr.materialClass),
                materialName = ocr.materialName,
                materialType = ocr.materialType,
                ocrRawText = ocr.ocrRawText,
                originPlace = ocr.originPlace,
                pieceWeight = FormatDecimal(ocr.pieceWeight),
                pieceWeightUnit = string.IsNullOrWhiteSpace(ocr.pieceWeightUnit) ? "吨" : ocr.pieceWeightUnit,
                spec = ocr.spec,
                strength = ocr.strength
            });
        }

        if (_ticketItems.Count == 0)
        {
            ClearSelectedTicket();
            return;
        }

        ApplySelectedTicket(_ticketItems[0]);
    }

    private void ApplyDetailList(IEnumerable<RawMaterialReceivingDetailItemDto> detailItems)
    {
        _ocrItems.Clear();
        foreach (var item in detailItems)
        {
            _ocrItems.Add(new RawMaterialOcrDto
            {
                qrCode = FirstNonEmpty(item.qrCode, item.coilNo, item.id),
                coilCount = FormatDecimal(item.count),
                furnaceNo = item.furnaceNo,
                materialClass = item.materialClass,
                materialClassName = ResolveMaterialClassName(item.materialClass),
                materialCode = item.materialCode,
                materialName = item.materialName,
                materialType = item.materialTypeDisplay,
                originPlace = item.origin,
                pieceWeight = FormatDecimal(item.instockQty ?? item.pieceWeight),
                pieceWeightUnit = string.IsNullOrWhiteSpace(item.unit) ? "吨" : item.unit,
                spec = item.spec,
                strength = item.weight
            });
        }

        MaterialListTitle.Text = $"待入库列表 ({_ocrItems.Count})";
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

            _pendingTicketAttachment = await _warehouseApi.UploadAttachmentAsync(photo, "toolingManager", "images");
        }
        catch (Exception ex)
        {
            await DisplayAlert("附件上传失败", ex.Message, "确定");
            return;
        }

        try
        {
            var ocr = await _warehouseApi.RecognizeIncomingAsync(_pendingTicketAttachment, _instockNo);
            ShowTicketConfirmDialog(ocr, false);
        }
        catch
        {
            ShowTicketConfirmDialog(new RawMaterialOcrDto(), true);
        }
    }

    private async Task<FileResult?> GetTicketPhotoAsync()
    {
        var capturePage = new TicketPhotoCapturePage();
        await Navigation.PushModalAsync(capturePage);
        return await capturePage.Completion;
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


    private static string? FormatDecimal(decimal? value) => value?.ToString("0.##", CultureInfo.InvariantCulture);

    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static decimal? ParseNullableDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return decimal.TryParse(value.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var invariantValue)
            ? invariantValue
            : decimal.TryParse(value.Trim(), NumberStyles.Any, CultureInfo.CurrentCulture, out var currentValue) ? currentValue : null;
    }

    private static int? ParseNullableInt(string? value)
    {
        var parsed = ParseNullableDecimal(value);
        return parsed.HasValue ? (int?)decimal.ToInt32(decimal.Truncate(parsed.Value)) : null;
    }

    private void OnCloseSummaryTapped(object sender, TappedEventArgs e) => SummaryOverlay.IsVisible = false;

    private void OnCloseSummaryClicked(object sender, EventArgs e) => SummaryOverlay.IsVisible = false;

    private async void OnScanBindClicked(object sender, EventArgs e)
    {
        if (_selectedTicket is null)
        {
            await DisplayAlert("提示", "请先手动录入并确认票签内容。", "确定");
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
        BindMaterialCodeEntry.Text = source.materialCode;
        SelectMaterialClass(BindMaterialTypePicker, source.materialClass);
        BindMaterialNameEntry.Text = source.materialName;
        BindSpecEntry.Text = source.spec;
        BindFurnaceNoEntry.Text = source.furnaceNo;
        BindOriginPlaceEntry.Text = source.originPlace;
        BindStrengthEntry.Text = source.strength;
        BindCoilCountEntry.Text = source.coilCount;
        BindCoilDiameterEntry.Text = source.coilDiameter;
        BindPieceWeightEntry.Text = source.pieceWeight;
        RefreshMaterialClassFormVisibility(true);
        BindConfirmOverlay.IsVisible = true;
    }

    private void OnConfirmBindClicked(object sender, EventArgs e)
    {
        var materialClass = GetSelectedMaterialClass(BindMaterialTypePicker, _selectedTicket?.materialClass);
        var bound = new RawMaterialOcrDto
        {
            qrCode = _pendingQrCode,
            materialClass = materialClass?.dictItemValue ?? _selectedTicket?.materialClass,
            materialClassName = materialClass?.dictItemName ?? ResolveMaterialClassName(_selectedTicket?.materialClass),
            materialType = materialClass?.dictItemValue ?? _selectedTicket?.materialType,
            materialCode = BindMaterialCodeEntry.Text,
            materialName = BindMaterialNameEntry.Text,
            spec = BindSpecEntry.Text,
            furnaceNo = BindFurnaceNoEntry.Text,
            originPlace = BindOriginPlaceEntry.Text,
            pieceWeight = BindPieceWeightEntry.Text,
            pieceWeightUnit = ResolvePieceWeightUnit(materialClass),
            coilCount = BindCoilCountEntry.Text,
            coilDiameter = BindCoilDiameterEntry.Text,
            ocrRawText = _selectedTicket?.ocrRawText,
            strength = BindStrengthEntry.Text
        };

        _ocrItems.Add(bound);
        MaterialListTitle.Text = $"待入库列表 ({_ocrItems.Count})";
        BindConfirmOverlay.IsVisible = false;
    }

    private void OnCloseBindConfirmTapped(object sender, TappedEventArgs e) => BindConfirmOverlay.IsVisible = false;

    private void OnCancelBindConfirmClicked(object sender, EventArgs e) => BindConfirmOverlay.IsVisible = false;

    private void OnTicketMaterialTypeChanged(object sender, EventArgs e) => RefreshMaterialClassFormVisibility(false);

    private void OnBindMaterialTypeChanged(object sender, EventArgs e) => RefreshMaterialClassFormVisibility(true);

    private void OnTicketMaterialTypePickerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Picker.SelectedItem) or nameof(Picker.SelectedIndex))
        {
            RefreshMaterialClassFormVisibility(false);
        }
    }

    private void OnBindMaterialTypePickerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Picker.SelectedItem) or nameof(Picker.SelectedIndex))
        {
            RefreshMaterialClassFormVisibility(true);
        }
    }

    private void RefreshMaterialClassFormVisibility(bool isBindDialog)
    {
        var picker = isBindDialog ? BindMaterialTypePicker : MaterialTypePicker;
        ApplyMaterialClassFormVisibility(picker, isBindDialog);
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(50), () => ApplyMaterialClassFormVisibility(picker, isBindDialog));
    }

    private void ApplyMaterialClassFormVisibility(Picker picker, bool isBindDialog)
    {
        var isSemiFinished = IsSemiFinished(picker);
        if (isBindDialog)
        {
            BindSemiFieldsRow1.IsVisible = isSemiFinished;
            BindSemiFieldsRow2.IsVisible = isSemiFinished;
            BindPieceWeightLabel.Text = isSemiFinished ? "实际件重（KG） *" : "实际件重（吨） *";
            return;
        }

        TicketSemiFieldsRow1.IsVisible = isSemiFinished;
        TicketSemiFieldsRow2.IsVisible = isSemiFinished;
        TicketPieceWeightLabel.Text = isSemiFinished ? "件重（KG） *" : "件重（吨） *";
    }

    private bool IsSemiFinished(Picker picker)
    {
        if (picker.SelectedItem is DictItemDto selected && IsSemiFinished(selected))
        {
            return true;
        }

        if (picker.SelectedIndex >= 0 && picker.SelectedIndex < _materialClassOptions.Count && IsSemiFinished(_materialClassOptions[picker.SelectedIndex]))
        {
            return true;
        }

        var selectedText = FirstNonEmpty(picker.SelectedItem?.ToString(),
            picker.SelectedIndex >= 0 && picker.SelectedIndex < picker.Items.Count ? picker.Items[picker.SelectedIndex] : null);
        return selectedText.Contains("半成品", StringComparison.OrdinalIgnoreCase) ||
            selectedText.Contains("semi", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSemiFinished(DictItemDto? materialClass)
    {
        var value = FirstNonEmpty(materialClass?.dictItemValue, materialClass?.dictItemName);
        return value.Contains("半成品", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("semi", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolvePieceWeightUnit(DictItemDto? materialClass) => IsSemiFinished(materialClass) ? "KG" : "吨";

    private void ShowTicketConfirmDialog(RawMaterialOcrDto ocr, bool showOcrFailedHint = false)
    {
        _pendingOcr = ocr;
        OcrFailedHintLabel.IsVisible = showOcrFailedHint;
        SelectMaterialClass(MaterialTypePicker, ocr.materialClass);
        MaterialCodeEntry.Text = ocr.materialCode;
        MaterialNameEntry.Text = ocr.materialName;
        SpecEntry.Text = ocr.spec;
        FurnaceNoEntry.Text = ocr.furnaceNo;
        OriginPlaceEntry.Text = ocr.originPlace;
        StrengthEntry.Text = ocr.strength;
        CoilCountEntry.Text = ocr.coilCount;
        CoilDiameterEntry.Text = ocr.coilDiameter;
        PieceWeightEntry.Text = ocr.pieceWeight;
        RefreshMaterialClassFormVisibility(false);
        TicketConfirmOverlay.IsVisible = true;
    }

    private async void OnConfirmTicketClicked(object sender, EventArgs e)
    {
        var materialClass = GetSelectedMaterialClass(MaterialTypePicker, _pendingOcr?.materialClass);
        _selectedTicket = new RawMaterialOcrDto
        {
            materialClass = materialClass?.dictItemValue ?? _pendingOcr?.materialClass,
            materialClassName = materialClass?.dictItemName ?? ResolveMaterialClassName(_pendingOcr?.materialClass),
            materialType = materialClass?.dictItemValue ?? _pendingOcr?.materialType,
            materialCode = MaterialCodeEntry.Text,
            materialName = MaterialNameEntry.Text,
            spec = SpecEntry.Text,
            furnaceNo = FurnaceNoEntry.Text,
            originPlace = OriginPlaceEntry.Text,
            pieceWeight = PieceWeightEntry.Text,
            pieceWeightUnit = ResolvePieceWeightUnit(materialClass),
            coilCount = CoilCountEntry.Text,
            coilDiameter = CoilDiameterEntry.Text,
            ocrRawText = _pendingOcr?.ocrRawText,
            strength = StrengthEntry.Text
        };

        try
        {
            await SaveOcrIncomingImageAsync(_selectedTicket);
        }
        catch (Exception ex)
        {
            await DisplayAlert("保存票签失败", ex.Message, "确定");
            return;
        }

        AddTicketAndSelect(_selectedTicket);
        TicketConfirmOverlay.IsVisible = false;
    }


    private async Task SaveOcrIncomingImageAsync(RawMaterialOcrDto ticket)
    {
        if (_pendingTicketAttachment is null || string.IsNullOrWhiteSpace(_instockNo))
        {
            return;
        }

        var success = await _warehouseApi.SaveOcrIncomingImageAsync(new SaveOcrIncomingImageRequestDto
        {
            coilCount = ParseNullableInt(ticket.coilCount),
            coilDiameter = ParseNullableDecimal(ticket.coilDiameter),
            fileInfo = _pendingTicketAttachment,
            furnaceNo = ticket.furnaceNo,
            instockNo = _instockNo,
            materialClass = ticket.materialClass,
            materialName = ticket.materialName,
            materialType = ticket.materialType,
            originPlace = ticket.originPlace,
            pieceWeight = ParseNullableDecimal(ticket.pieceWeight),
            spec = ticket.spec,
            strength = ticket.strength
        });

        if (success is false)
        {
            throw new InvalidOperationException("保存OCR识别图片接口返回失败。");
        }

        _pendingTicketAttachment = null;
    }

    private void SelectMaterialClass(Picker picker, string? value)
    {
        var option = FindMaterialClassOption(value);
        if (option is not null)
        {
            picker.SelectedItem = option;
            return;
        }

        if (_materialClassOptions.Count > 0)
        {
            picker.SelectedIndex = 0;
            picker.SelectedItem = _materialClassOptions[0];
        }
    }

    private DictItemDto? GetSelectedMaterialClass(Picker picker, string? fallbackValue)
    {
        if (picker.SelectedItem is DictItemDto selected)
        {
            return selected;
        }

        return FindMaterialClassOption(fallbackValue) ?? _materialClassOptions.FirstOrDefault();
    }

    private DictItemDto? FindMaterialClassOption(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        return _materialClassOptions.FirstOrDefault(option =>
            string.Equals(option.dictItemValue, value, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(option.dictItemName, value, StringComparison.OrdinalIgnoreCase));
    }

    private string? ResolveMaterialClassName(string? value) => FindMaterialClassOption(value)?.dictItemName ?? value;

    private static List<DictItemDto> CreateDefaultMaterialClassOptions() => new()
    {
        new DictItemDto { dictItemName = "原料", dictItemValue = "raw_material" },
        new DictItemDto { dictItemName = "半成品", dictItemValue = "semi_finished" }
    };

    private void AddTicketAndSelect(RawMaterialOcrDto ticket)
    {
        _ticketItems.Add(ticket);
        ApplySelectedTicket(ticket);
    }

    private void ApplySelectedTicket(RawMaterialOcrDto ticket)
    {
        foreach (var item in _ticketItems)
        {
            item.isSelected = ReferenceEquals(item, ticket);
        }

        _selectedTicket = ticket;
        ExtractedTextLabel.IsVisible = false;
        TicketList.IsVisible = true;
        TicketList.SelectedItem = ticket;
        SelectedTicketCard.IsVisible = false;
        SelectedMaterialTypeLabel.Text = ticket.materialClassDisplay;
        SelectedMaterialNameLabel.Text = ticket.materialNameDisplay;
        SelectedSpecLabel.Text = ticket.specDisplay;
        SelectedFurnaceNoLabel.Text = ticket.furnaceNoDisplay;
        SelectedOriginPlaceLabel.Text = ticket.originPlaceDisplay;
        SelectedPieceWeightLabel.Text = ticket.pieceWeightDisplay;
        var isSemiFinished = ticket.isSemiFinished;
        SelectedStrengthTitleLabel.IsVisible = isSemiFinished;
        SelectedStrengthLabel.IsVisible = isSemiFinished;
        SelectedStrengthLabel.Text = ticket.strengthDisplay;
        SelectedCoilCountTitleLabel.IsVisible = isSemiFinished;
        SelectedCoilCountLabel.IsVisible = isSemiFinished;
        SelectedCoilCountLabel.Text = ticket.coilCountDisplay;
        SelectedCoilDiameterTitleLabel.IsVisible = isSemiFinished;
        SelectedCoilDiameterLabel.IsVisible = isSemiFinished;
        SelectedCoilDiameterLabel.Text = ticket.coilDiameterDisplay;
    }

    private void ClearSelectedTicket()
    {
        _selectedTicket = null;
        _ticketItems.Clear();
        TicketList.SelectedItem = null;
        TicketList.IsVisible = false;
        SelectedTicketCard.IsVisible = false;
        ExtractedTextLabel.IsVisible = true;
        ExtractedTextLabel.Text = "暂无票签内容，请先手动录入";
    }


    private void OnTicketSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is RawMaterialOcrDto ticket)
        {
            ApplySelectedTicket(ticket);
        }
    }

    private void OnDeleteTicketClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: RawMaterialOcrDto ticket })
        {
            return;
        }

        _ticketItems.Remove(ticket);
        if (ReferenceEquals(_selectedTicket, ticket))
        {
            if (_ticketItems.Count > 0)
            {
                ApplySelectedTicket(_ticketItems[0]);
            }
            else
            {
                ClearSelectedTicket();
            }
        }
    }

    private void OnDeleteTicketTapped(object sender, TappedEventArgs e) => ClearSelectedTicket();

    private void OnCloseTicketConfirmTapped(object sender, TappedEventArgs e) => TicketConfirmOverlay.IsVisible = false;

    private void OnCancelTicketConfirmClicked(object sender, EventArgs e) => TicketConfirmOverlay.IsVisible = false;


    private WarehouseInfoDto? GetSelectedWarehouse()
    {
        if (WarehousePicker.SelectedItem is WarehouseInfoDto selected)
        {
            return selected;
        }

        if (WarehousePicker.SelectedIndex >= 0 && WarehousePicker.SelectedIndex < _warehouses.Count)
        {
            return _warehouses[WarehousePicker.SelectedIndex];
        }

        return _warehouses.FirstOrDefault();
    }

    private async void OnSubmitInstockClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_instockNo))
        {
            await DisplayAlert("提示", "入库单号尚未生成，请稍后重试。", "确定");
            return;
        }

        var warehouse = GetSelectedWarehouse();
        if (warehouse is null || string.IsNullOrWhiteSpace(warehouse.selectedName) || string.IsNullOrWhiteSpace(warehouse.selectedCode))
        {
            await DisplayAlert("提示", "请选择有效的入库仓库。", "确定");
            return;
        }

        if (_ocrItems.Count == 0)
        {
            await DisplayAlert("提示", "请先扫码绑定至少一条待入库物料。", "确定");
            return;
        }

        //var missingRequired = _ocrItems.FirstOrDefault(item =>
        //    string.IsNullOrWhiteSpace(item.qrCode) ||
        //    string.IsNullOrWhiteSpace(item.materialClass) ||
        //    string.IsNullOrWhiteSpace(item.materialCode) ||
        //    ParseWeight(item.pieceWeight) <= 0m);
        //if (missingRequired is not null)
        //{
        //    await DisplayAlert("提示", "请确认每条明细都已填写二维码、物料分类、物料编码和有效件重。", "确定");
        //    return;
        //}

        try
        {
            var request = new QuickInstockRequestDto
            {
                detailList = _ocrItems.Select((item, index) => new QuickInstockDetailDto
                {
                    coilCount = ParseNullableInt(item.coilCount),
                    coilDiameter = ParseNullableDecimal(item.coilDiameter),
                    count = 1,
                    countSeq = index + 1,
                    furnaceNo = item.furnaceNo,
                    instockNo = _instockNo,
                    instockQty = ParseWeight(item.pieceWeight),
                    instockWarehouse = warehouse.selectedName,
                    instockWarehouseCode = warehouse.selectedCode,
                    materialClass = item.materialClass,
                    materialCode = item.materialCode,
                    materialName = item.materialName,
                    origin = item.originPlace,
                    qrCode = item.qrCode,
                    spec = item.spec,
                    strength = item.strength,
                    unit = "吨"
                }).ToList()
            };

            var success = await _warehouseApi.QuickInstockAsync(request);
            if (success is false)
            {
                await DisplayAlert("提交失败", "接口返回失败，请稍后重试。", "确定");
                return;
            }

            await DisplayAlert("提交成功", "采购入库已提交。", "确定");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await DisplayAlert("提交失败", ex.Message, "确定");
        }
    }

    private async void OnBackLabelTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnCancelClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}

internal sealed record MaterialSummaryItem(string materialName, string materialType, string originPlace, int count, decimal totalWeight)
{
    public string totalWeightDisplay => $"{totalWeight:0.00} 吨";
}
