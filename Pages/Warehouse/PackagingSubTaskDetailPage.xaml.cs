using JXHLJSApp.Services;
using JXHLJSApp.Services.Warehouse;

namespace JXHLJSApp.Pages.Warehouse;

[QueryProperty(nameof(TaskId), "id")]
public partial class PackagingSubTaskDetailPage : ContentPage
{
    private readonly IWarehouseApi _warehouseApi;
    private readonly IScanService _scanService;
    private string? _id;

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
            TaskNoLabel.Text = detail.taskNoDisplay;
            MaterialLabel.Text = $"{detail.materialNameDisplay} {detail.materialCodeDisplay}";
            TemplateLabel.Text = "标准成品标签_v2";
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

    private async void OnScanClicked(object sender, EventArgs e)
    {
        var code = await _scanService.ScanAsync("包装扫码");
        if (!string.IsNullOrWhiteSpace(code))
        {
            ActualWeightEntry.Text = code.Trim();
        }
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
