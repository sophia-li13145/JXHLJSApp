using System.Globalization;
using JXHLJSApp.Models.Warehouse;
using JXHLJSApp.Services;
using JXHLJSApp.Services.Warehouse;

namespace JXHLJSApp.Pages.Warehouse;

[QueryProperty(nameof(InstockNo), nameof(InstockNo))]
public partial class RawMaterialReceivingDetailPage : ContentPage
{
    private readonly IWarehouseApi _warehouseApi;
    private string? _instockNo;
    private bool _isOpeningAttachment;
    private IReadOnlyList<RawMaterialReceivingDetailItemDto> _detailItems = [];

    public string? InstockNo
    {
        get => _instockNo;
        set => _instockNo = Uri.UnescapeDataString(value ?? string.Empty);
    }

    public RawMaterialReceivingDetailPage(IWarehouseApi warehouseApi)
    {
        InitializeComponent();
        _warehouseApi = warehouseApi;
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
        await LoadDetailAsync();
    }

    private async void OnRefreshing(object sender, EventArgs e) => await LoadDetailAsync();

    private async Task LoadDetailAsync()
    {
        if (string.IsNullOrWhiteSpace(_instockNo))
        {
            await DisplayAlert("提示", "未找到入库单号。", "确定");
            return;
        }

        try
        {
            RefreshContainer.IsRefreshing = true;
            var detail = await _warehouseApi.GetRawMaterialReceivingDetailAsync(_instockNo);
            InstockNoLabel.Text = detail.instockNoDisplay;
            StatusLabel.Text = detail.statusDisplay;
            InstockDateLabel.Text = detail.instockDateDisplay;
            WarehouseLabel.Text = detail.warehouseDisplay;
            LocationLabel.Text = detail.locationDisplay;
            _detailItems = detail.detailItems;
            DetailTitleLabel.Text = $"入库明细 (共 {_detailItems.Count} 件)";
            DetailList.ItemsSource = _detailItems;
            CalculateSummaryButton.IsEnabled = _detailItems.Count > 0;
            var attachments = detail.attachments;
            AttachmentEmptyLabel.IsVisible = attachments.Count == 0;
            AttachmentLayout.IsVisible = attachments.Count > 0;
            BindableLayout.SetItemsSource(AttachmentLayout, attachments);
            await Task.Yield();
            await LoadAttachmentPreviewsAsync(attachments);
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "加载失败", ex.Message, "确定");
        }
        finally
        {
            RefreshContainer.IsRefreshing = false;
        }
    }

    private void OnCalculateSummaryClicked(object sender, EventArgs e)
    {
        var summaries = _detailItems
            .GroupBy(item => new
            {
                materialName = item.materialNameDisplay.Trim(),
                materialType = item.materialTypeDisplay.Trim(),
                originPlace = item.originDisplay.Trim(),
                weightUnit = NormalizeWeightUnit(item)
            })
            .Select(group => new MaterialSummaryItem(
                group.Key.materialName,
                group.Key.materialType,
                group.Key.originPlace,
                group.Count(),
                group.Sum(item => item.instockQty ?? item.pieceWeight ??
                    (decimal.TryParse(item.weight, NumberStyles.Number, CultureInfo.InvariantCulture, out var weight)
                        ? weight
                        : 0m)),
                group.Key.weightUnit))
            .OrderBy(item => item.materialName)
            .ThenBy(item => item.materialType)
            .ThenBy(item => item.originPlace)
            .ToList();

        SummaryOverlay.Show("入库明细汇总台账", summaries);
    }

    private static string NormalizeWeightUnit(RawMaterialReceivingDetailItemDto item)
    {
        var unit = item.unit?.Trim();
        return unit?.ToUpperInvariant() switch
        {
            "KG" or "千克" or "公斤" => "KG",
            "T" or null or "" => "吨",
            _ => unit
        };
    }

    protected override bool OnBackButtonPressed()
    {
        if (SummaryOverlay.IsVisible)
        {
            SummaryOverlay.IsVisible = false;
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private async Task LoadAttachmentPreviewsAsync(IEnumerable<AttachmentDto> attachments)
    {
        await Task.WhenAll(attachments.Select(async attachment =>
        {
            attachment.previewImage = null;
            attachment.previewLoadFailed = false;
            attachment.isPreviewLoading = true;

            try
            {
                var bytes = await _warehouseApi.GetAttachmentPreviewBytesAsync(attachment.attachmentUrl!);
                if (bytes is not { Length: > 0 })
                {
                    throw new InvalidOperationException("图片内容为空。");
                }

                attachment.previewImage = ImageSource.FromStream(
                    () => new MemoryStream(bytes, writable: false));
            }
            catch (Exception ex)
            {
                // 单个附件预览失败不应阻止入库详情及其他附件展示，点击时仍可重试。
                Serilog.Log.Error(
                    ex,
                    "采购入库附件预览加载失败，attachmentUrl={AttachmentUrl}",
                    attachment.attachmentUrl);
                attachment.previewImage = null;
                attachment.previewLoadFailed = true;
            }
            finally
            {
                attachment.isPreviewLoading = false;
            }
        }));
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnAttachmentTapped(object sender, TappedEventArgs e)
    {
        if (_isOpeningAttachment || e.Parameter is not AttachmentDto attachment)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(attachment.attachmentUrl))
        {
            await DisplayAlert("提示", "该附件没有可用的预览地址。", "确定");
            return;
        }

        try
        {
            _isOpeningAttachment = true;
            var previewUrl = attachment.previewUrl ??
                await _warehouseApi.PreviewAttachmentAsync(attachment.attachmentUrl);
            attachment.previewUrl = previewUrl;
            if (string.IsNullOrWhiteSpace(previewUrl) ||
                !Uri.TryCreate(previewUrl, UriKind.Absolute, out var previewUri))
            {
                await DisplayAlert("预览失败", "文件预览接口未返回有效地址。", "确定");
                return;
            }

            if (!await Launcher.Default.TryOpenAsync(previewUri))
            {
                await DisplayAlert("预览失败", "当前设备无法打开该附件。", "确定");
            }
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "预览失败", ex.Message, "确定");
        }
        finally
        {
            _isOpeningAttachment = false;
        }
    }
}
