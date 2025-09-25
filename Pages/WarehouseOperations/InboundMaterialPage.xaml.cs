using IndustrialControlMAUI.Models;
using IndustrialControlMAUI.Services;
using IndustrialControlMAUI.ViewModels;
using SharedLocationVM = IndustrialControlMAUI.ViewModels.LocationVM;


namespace IndustrialControlMAUI.Pages;

[QueryProperty(nameof(InstockId), "instockId")]
[QueryProperty(nameof(InstockNo), "instockNo")]
[QueryProperty(nameof(OrderType), "orderType")]
[QueryProperty(nameof(OrderTypeName), "orderTypeName")]
[QueryProperty(nameof(ArrivalNo), "arrivalNo")]
[QueryProperty(nameof(PurchaseNo), "purchaseNo")]
[QueryProperty(nameof(SupplierName), "supplierName")]
[QueryProperty(nameof(CreatedTime), "createdTime")]
public partial class InboundMaterialPage : ContentPage
{
    //private readonly ScanService _scanSvc;
    private readonly InboundMaterialViewModel _vm;
    public string? InstockId { get; set; }
    public string? InstockNo { get; set; }
    public string? OrderType { get; set; }
    public string? OrderTypeName { get; set; }
    public string? ArrivalNo { get; set; }
    public string? PurchaseNo { get; set; }
    public string? SupplierName { get; set; }
    public string? CreatedTime { get; set; }
    private readonly IDialogService _dialogs;
    private bool _loadedOnce;
    private readonly IServiceProvider _sp;

    public InboundMaterialPage(IServiceProvider sp, InboundMaterialViewModel vm,IDialogService dialogs)
    {
        InitializeComponent();
        _sp = sp;
        BindingContext = vm;
        //_scanSvc = scanSvc;
        _vm = vm;
        _dialogs = dialogs;
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
        if (!string.IsNullOrWhiteSpace(InstockId))
        {
            await _vm.InitializeFromSearchAsync(
                instockId: InstockId ?? "",
                instockNo: InstockNo ?? "",
                orderType: OrderType ?? "",
                orderTypeName: OrderTypeName ?? "",
                purchaseNo: PurchaseNo ?? "",
                arrivalNo: ArrivalNo ?? "",
                supplierName: SupplierName ?? "",
                createdTime: CreatedTime ?? ""
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
        var ok = await _vm.ConfirmInboundAsync();
        if (ok)
        {
            await DisplayAlert("提示", "入库成功", "确定");
            _vm.ClearAll();

            // ✅ 返回到工单查询页面（InboundMaterialSearchPage）
            await Shell.Current.GoToAsync(nameof(InboundMaterialSearchPage));
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
