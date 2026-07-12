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
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        try
        {
            var material = await _workOrderApi.ScanQueryMaterialInfoAsync(code.Trim());
            ApplyScannedMaterial(material);
        }
        catch (Exception ex)
        {
            await DisplayAlert("包装扫码失败", ex.Message, "确定");
        }
    }

    private void ApplyScannedMaterial(MaterialQrCodeInfoDto material)
    {
        ScanSuccessPanel.IsVisible = true;
        ScannedMaterialPanel.IsVisible = true;

        ScannedMaterialCodeLabel.Text = $"物料编号：{Display(material.materialCode)}";
        ScannedSteelGradeLabel.Text = Display(material.steelGrade ?? material.materialName);
        ScannedSpecLabel.Text = Display(material.specification ?? material.spec);
        ScannedOriginLabel.Text = Display(material.originPlace);
        ScannedLengthLabel.Text = FormatQuantity(material.length, material.lengthUnit);
        ScannedWeightLabel.Text = FormatQuantity(material.weight, material.weightUnit ?? material.unit ?? "KG");
        ActualWeightEntry.Text = material.weight?.ToString("0.##") ?? string.Empty;
    }

    private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "--" : value!;

    private static string FormatQuantity(decimal? value, string? unit)
    {
        if (!value.HasValue)
        {
            return "--";
        }

        var text = value.Value % 1 == 0 ? value.Value.ToString("0") : value.Value.ToString("0.##");
        return string.IsNullOrWhiteSpace(unit) ? text : $"{text} {unit}";
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
