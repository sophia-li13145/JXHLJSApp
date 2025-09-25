using IndustrialControlMAUI.Models;
using IndustrialControlMAUI.Services;
using IndustrialControlMAUI.ViewModels;
using SharedLocationVM = IndustrialControlMAUI.ViewModels.LocationVM;

namespace IndustrialControlMAUI.Pages;

[QueryProperty(nameof(OutstockId), "outstockId")]
[QueryProperty(nameof(OutstockNo), "outstockNo")]
[QueryProperty(nameof(RequisitionMaterialNo), "requisitionMaterialNo")]
[QueryProperty(nameof(WorkOrderNo), "workOrderNo")]
[QueryProperty(nameof(Memo), "memo")]
public partial class OutboundMaterialPage : ContentPage
{
    //private readonly ScanService _scanSvc;
    private readonly OutboundMaterialViewModel _vm;
    public string? OutstockId { get; set; }
    public string? OutstockNo { get; set; }
    public string? RequisitionMaterialNo { get; set; }
    public string? WorkOrderNo { get; set; }
    public string? Memo { get; set; }
    private readonly IDialogService _dialogs;
    private readonly IServiceProvider _sp;

    public OutboundMaterialPage(IServiceProvider sp, OutboundMaterialViewModel vm, IDialogService dialogs)
    {
        InitializeComponent();
        BindingContext = vm;
        //_scanSvc = scanSvc;
        _vm = vm;
        _dialogs = dialogs;
        _sp = sp;
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
                requisitionMaterialNo: RequisitionMaterialNo ?? "",
                workOrderNo: WorkOrderNo ?? "",
                memo: Memo ?? ""
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
       // _scanSvc.StopListening();

        base.OnDisappearing();
    }

    private void OnScanned(string data, string type)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // 常见处理：自动填入单号/条码并触发查询或加入明细
            _vm.ScanCode = data;

            // 你原本的逻辑：若识别到是订单号 → 查询；若是包装码 → 加入列表等
            await _vm.HandleScannedAsync(data, type);
        });
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

            // ✅ 返回到工单查询页面（InboundMaterialSearchPage）
            await Shell.Current.GoToAsync(nameof(OutboundMaterialSearchPage));
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
