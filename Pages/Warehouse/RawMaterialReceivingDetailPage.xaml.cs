using JXHLJSApp.Models.Warehouse;
using JXHLJSApp.Services.Warehouse;
using Microsoft.Maui.ApplicationModel;

namespace JXHLJSApp.Pages.Warehouse;

[QueryProperty(nameof(InstockNo), nameof(InstockNo))]
public partial class RawMaterialReceivingDetailPage : ContentPage
{
    private readonly IWarehouseApi _warehouseApi;
    private string? _instockNo;

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
            DetailTitleLabel.Text = $"入库明细 (共 {detail.detailItems.Count} 件)";
            DetailList.ItemsSource = detail.detailItems;
            AttachmentList.ItemsSource = detail.mainAttachments;
        }
        catch (Exception ex)
        {
            await DisplayAlert("加载失败", ex.Message, "确定");
        }
        finally
        {
            RefreshContainer.IsRefreshing = false;
        }
    }

    private async void OnAttachmentTapped(object sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not AttachmentDto attachment)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(attachment.attachmentUrl))
        {
            await DisplayAlert("提示", "当前附件暂无可预览图片。", "确定");
            return;
        }

        try
        {
            var previewUrl = await _warehouseApi.PreviewAttachmentAsync(attachment.attachmentUrl);
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

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
