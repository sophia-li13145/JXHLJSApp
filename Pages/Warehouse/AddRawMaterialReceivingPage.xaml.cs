using System.Collections.ObjectModel;
using JXHLJSApp.Models.Warehouse;
using JXHLJSApp.Services.Warehouse;

namespace JXHLJSApp.Pages.Warehouse;

public partial class AddRawMaterialReceivingPage : ContentPage
{
    private readonly IWarehouseApi _warehouseApi;
    private readonly ObservableCollection<RawMaterialOcrDto> _ocrItems = new();
    private RawMaterialOcrDto? _pendingOcr;
    private RawMaterialOcrDto? _selectedTicket;
    private string? _instockNo;

    public AddRawMaterialReceivingPage(IWarehouseApi warehouseApi)
    {
        InitializeComponent();
        _warehouseApi = warehouseApi;
        OcrList.ItemsSource = _ocrItems;
        MaterialTypePicker.ItemsSource = new[] { "原料", "半成品" };
        MaterialTypePicker.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
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

            var warehouses = await _warehouseApi.QueryWarehouseInfoAsync();
            WarehousePicker.ItemsSource = warehouses;
            if (warehouses.Count > 0)
            {
                WarehousePicker.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("初始化失败", ex.Message, "确定");
        }
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
            var permission = await Permissions.RequestAsync<Permissions.Camera>();
            if (permission != PermissionStatus.Granted)
            {
                await DisplayAlert("提示", "未授予摄像头权限。", "确定");
                return;
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions { Title = "拍摄票签" });
            if (photo is null) return;

            ExtractedTextLabel.Text = "图片上传与识别中...";
            var attachment = await _warehouseApi.UploadAttachmentAsync(photo, "rawMaterialReceiving", _instockNo);
            var ocr = await _warehouseApi.RecognizeIncomingAsync(attachment, _instockNo);
            ShowTicketConfirmDialog(ocr);
        }
        catch (FeatureNotSupportedException)
        {
            await DisplayAlert("提示", "当前设备不支持拍照。", "确定");
        }
        catch (Exception ex)
        {
            ExtractedTextLabel.Text = "暂无提取的票签内容";
            await DisplayAlert("识别失败", ex.Message, "确定");
        }
    }

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
            strength = _pendingOcr?.strength
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

    private void OnDeleteTicketTapped(object sender, TappedEventArgs e) => ClearSelectedTicket();

    private void OnCloseTicketConfirmTapped(object sender, TappedEventArgs e) => TicketConfirmOverlay.IsVisible = false;

    private void OnCancelTicketConfirmClicked(object sender, EventArgs e) => TicketConfirmOverlay.IsVisible = false;

    private async void OnBackLabelTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnCancelClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
