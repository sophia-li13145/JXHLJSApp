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
            DetailTitleLabel.Text = $"入库明细 (共 {detail.detailItems.Count} 件)";
            DetailList.ItemsSource = detail.detailItems;
            var attachments = detail.attachments;
            AttachmentEmptyLabel.IsVisible = attachments.Count == 0;
            AttachmentLayout.IsVisible = attachments.Count > 0;
            await LoadAttachmentPreviewsAsync(attachments);
            BindableLayout.SetItemsSource(AttachmentLayout, attachments);
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

    private async Task LoadAttachmentPreviewsAsync(IEnumerable<AttachmentDto> attachments)
    {
        await Task.WhenAll(attachments.Select(async attachment =>
        {
            try
            {
                var bytes = await _warehouseApi.DownloadAttachmentPreviewAsync(attachment.attachmentUrl!);
                if (bytes is { Length: > 0 })
                {
                    attachment.previewImage = ImageSource.FromStream(() => new MemoryStream(bytes));
                }
            }
            catch
            {
                // 单个附件预览失败不应阻止入库详情及其他附件展示，点击时仍可重试。
                attachment.previewImage = null;
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
            var previewUrl = await _warehouseApi.PreviewAttachmentAsync(attachment.attachmentUrl);
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
