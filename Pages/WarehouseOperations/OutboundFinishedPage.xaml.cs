using IndustrialControlMAUI.Models;
using IndustrialControlMAUI.Services;
using IndustrialControlMAUI.ViewModels;
using SharedLocationVM = IndustrialControlMAUI.ViewModels.LocationVM;

namespace IndustrialControlMAUI.Pages;

[QueryProperty(nameof(OutstockId), "outstockId")]
[QueryProperty(nameof(OutstockNo), "outstockNo")]
[QueryProperty(nameof(DeliveryNo), "deliveryNo")]
[QueryProperty(nameof(Customer), "customer")]
[QueryProperty(nameof(ExpectedDeliveryTime), "expectedDeliveryTime")]
[QueryProperty(nameof(SaleNo), "saleNo")]
[QueryProperty(nameof(DeliveryMemo), "deliveryMemo")]
public partial class OutboundFinishedPage : ContentPage
{
    //private readonly ScanService _scanSvc;
    private readonly OutboundFinishedViewModel _vm;
    private readonly IServiceProvider _sp;
    public string? OutstockId { get; set; }
    public string? OutstockNo { get; set; }
    public string? Customer { get; set; }
    public string? ExpectedDeliveryTime { get; set; }
    public string? DeliveryNo { get; set; }
    public string? SaleNo { get; set; }
    public string? DeliveryMemo { get; set; }
    private readonly IDialogService _dialogs;

    public OutboundFinishedPage(IServiceProvider sp, OutboundFinishedViewModel vm,IDialogService dialogs)
    {
        InitializeComponent();
        BindingContext = vm;
        //_scanSvc = scanSvc;
        _vm = vm;
        _dialogs = dialogs;
        _sp = sp;
        // 可选：配置前后缀与防抖
        //_scanSvc.Prefix = null;     // 例如 "}q" 之类的前缀；没有就留 null
        // _scanSvc.Suffix = "\n";     // 如果设备会附带换行，可去掉；没有就设 null
        //_scanSvc.DebounceMs = 250;
        //_scanSvc.Suffix = null;   // 先关掉
        //_scanSvc.DebounceMs = 0;  // 先关掉
    }

    private async void OnScanEntryCompleted(object? sender, EventArgs e)
    {
        // 取输入框内容
        var code = ScanEntry?.Text?.Trim();

        // 可选：空码直接返回
        if (string.IsNullOrWhiteSpace(code))
        {
            // 也可以静默返回，不弹提示
            return;
        }

        // 交给 VM 统一处理（第二个参数随意标记来源）
        await _vm.HandleScannedAsync(code!, "KEYBOARD");

        // 清空并继续聚焦，方便下一次输入/扫码
        ScanEntry.Text = string.Empty;
        ScanEntry.Focus();
    }


    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // ✅ 用搜索页带过来的基础信息初始化页面，并拉取两张表
        if (!string.IsNullOrWhiteSpace(OutstockId))
        {
            await _vm.InitializeFromSearchAsync(
                outstockId: OutstockId ?? "",
                outstockNo: OutstockNo ?? "",
                deliveryNo: DeliveryNo ?? "",
                customer: Customer ?? "",
                expectedDeliveryTime: ExpectedDeliveryTime ?? "",
                saleNo: SaleNo ?? "",
                deliveryMemo: DeliveryMemo ?? ""
            );
        }

        //_scanSvc.Scanned += OnScanned;
        //_scanSvc.StartListening();
        //_scanSvc.Attach(ScanEntry);
        ScanEntry.Focus();
    }


    /// <summary>
    /// 清空扫描记录
    /// </summary>
    void OnClearClicked(object sender, EventArgs e)
    {
        _vm.ClearScan();
        ScanEntry.Text = string.Empty;
        ScanEntry.Focus();
    }

    protected override void OnDisappearing()
    {
        // 退出页面即注销（防止多个程序/页面抢处理）
        //_scanSvc.Scanned -= OnScanned;
        //_scanSvc.StopListening();

        base.OnDisappearing();
    }


    /// <summary>
    /// 确认入库按钮点击
    /// </summary>
    async void OnConfirmClicked(object sender, EventArgs e)
    {
        var ok = await _vm.ConfirmOutboundAsync();
        if (ok)
        {
            await DisplayAlert("提示", "入库成功", "确定");
            _vm.ClearAll();

            // ✅ 返回到工单查询页面（InboundFinishedSearchPage）
            await Shell.Current.GoToAsync(nameof(OutboundFinishedSearchPage));
        }
        else
        {
            await DisplayAlert("提示", "入库失败，请检查数据", "确定");
        }
    }


    private async void OnBinTapped(object? sender, TappedEventArgs e)
    {
        // 1) 先拿到行对象并强转为 OutScannedItem
        if ((sender as BindableObject)?.BindingContext is not IndustrialControlMAUI.ViewModels.OutScannedItem item)
            return;

        // 2) 未扫描通过则提示并返回
        if (!item.ScanStatus)   // 注意这里用 ! 而不是 =
        {
            await DisplayAlert("提示", "该行未扫描通过，不能修改库位。", "确定");
            return;
        }
        // 用 B 方案的 ShowAsync（不需要 ServiceHelper）
        SharedLocationVM? picked = await WarehouseLocationPickerPage.ShowAsync(_sp, this);
        if (picked is null) return;

        var mapped = new BinInfo
        {
            WarehouseCode = picked.WarehouseCode,
            WarehouseName = picked.WarehouseName,
            ZoneCode = picked.Zone,
            RackCode = picked.Rack,
            LayerCode = picked.Layer,
            Location = picked.Location,
            InventoryStatus = picked.InventoryStatus,
            InStock = string.Equals(picked.InventoryStatus, "instock", StringComparison.OrdinalIgnoreCase)
        };

        await _vm.UpdateRowLocationAsync(item, mapped);
        _vm.ShowScannedCommand.Execute(null);
    }

    private async void OnQtyCompleted(object sender, EventArgs e)
    {
        if (sender is not Entry entry) return;
        if (entry.BindingContext is not IndustrialControlMAUI.ViewModels.OutScannedItem row) return;

        // 只看 ScanStatus：未通过则不提交
        if (!row.ScanStatus)
        {
            await DisplayAlert("提示", "该行尚未扫描通过，不能修改数量。", "确定");
            return;
        }

        await _vm.UpdateQuantityForRowAsync(row);
    }

}
