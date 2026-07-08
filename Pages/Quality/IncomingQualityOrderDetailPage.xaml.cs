using JXHLJSApp.Models.Quality;
using JXHLJSApp.Services;
using JXHLJSApp.Services.Quality;
using System.Collections.ObjectModel;

namespace JXHLJSApp.Pages.Quality;

[QueryProperty(nameof(IncomingQualityNo), "incomingQualityNo")]
public partial class IncomingQualityOrderDetailPage : ContentPage
{
    private readonly IQualityApi _qualityApi;
    private readonly IScanService _scanService;
    private readonly ObservableCollection<IncomingQualityScanDetailDto> _records = new();
    private string? _incomingQualityNo;

    public string? IncomingQualityNo
    {
        get => _incomingQualityNo;
        set => _incomingQualityNo = Uri.UnescapeDataString(value ?? string.Empty);
    }

    public IncomingQualityOrderDetailPage(IQualityApi qualityApi, IScanService scanService)
    {
        InitializeComponent();
        _qualityApi = qualityApi;
        _scanService = scanService;
        ScanRecordList.ItemsSource = _records;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDetailAsync();
    }

    private async void OnRefreshing(object sender, EventArgs e) => await LoadDetailAsync();

    private async Task LoadDetailAsync()
    {
        if (string.IsNullOrWhiteSpace(_incomingQualityNo))
        {
            await DisplayAlert("提示", "来料质检单号为空，无法查询详情。", "确定");
            return;
        }

        try
        {
            RefreshContainer.IsRefreshing = true;
            var detail = await _qualityApi.GetIncomingQualityOrderDetailAsync(_incomingQualityNo);
            QualityNoSpan.Text = detail.incomingQualityNoDisplay;
            InstockNoSpan.Text = detail.instockNoDisplay;
            StatusLabel.Text = detail.statusDisplay;
            ScanCountLabel.Text = $"共 {detail.scanCount} 次";
            _records.Clear();
            foreach (var record in detail.detailList ?? new List<IncomingQualityScanDetailDto>())
            {
                _records.Add(record);
            }
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

    private async void OnScanClicked(object sender, EventArgs e)
    {
        var code = await _scanService.ScanAsync("来料质检扫码");
        if (string.IsNullOrWhiteSpace(code)) return;

        try
        {
            var material = await _qualityApi.ScanIncomingQualityMaterialAsync(code.Trim());
            await Shell.Current.GoToAsync($"{AppShell.RouteIncomingQualityScan}?incomingQualityNo={Uri.EscapeDataString(_incomingQualityNo ?? string.Empty)}&qrCode={Uri.EscapeDataString(material.qrCode ?? code.Trim())}");
        }
        catch (Exception ex)
        {
            await DisplayAlert("扫码失败", ex.Message, "确定");
        }
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_incomingQualityNo))
        {
            await DisplayAlert("提示", "来料质检单号为空，无法删除。", "确定");
            return;
        }

        var confirm = await DisplayAlert("确认删除", $"确定删除来料质检单 {_incomingQualityNo} 吗？", "删除", "取消");
        if (!confirm) return;

        try
        {
            var deleted = await _qualityApi.DeleteIncomingQualityOrderAsync(_incomingQualityNo);
            if (!deleted)
            {
                await DisplayAlert("删除失败", "接口未返回删除成功，请稍后重试。", "确定");
                return;
            }

            await DisplayAlert("删除成功", "来料质检单已删除。", "确定");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await DisplayAlert("删除失败", ex.Message, "确定");
        }
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
