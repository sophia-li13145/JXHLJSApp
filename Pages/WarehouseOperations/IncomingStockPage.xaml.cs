using JXHLJSApp.ViewModels;

namespace JXHLJSApp.Pages;

public partial class IncomingStockPage : ContentPage
{
    private readonly IncomingStockViewModel _vm;
    private readonly IServiceProvider _sp;

    /// <summary>执行 IncomingStockPage 初始化逻辑。</summary>
    public IncomingStockPage(IServiceProvider sp, IncomingStockViewModel vm)
    {
        InitializeComponent();
        _sp = sp;
        BindingContext = _vm = vm;
    }

    private async void OnAddIncomingClicked(object sender, EventArgs e)
    {
        var result = await IncomingStockAddPopupPage.ShowAsync(_sp);
        if (result is null) return;
        _vm.AddLine(result);
    }

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        var ok = await _vm.SubmitPendingAsync();
        if (!ok) return;

        await DisplayAlert("提示", "待入库提交成功", "确定");
        _vm.ClearAll();
    }

    private async void OnScanEntryCompleted(object sender, EventArgs e)
    {
        var code = ScanEntry?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(code)) return;

        ScanEntry.Text = string.Empty;
        var result = await IncomingStockAddPopupPage.ShowAsync(_sp, code);
        if (result is null) return;
        _vm.AddLine(result);
    }

    private async void OnScanClicked(object sender, EventArgs e)
    {
        var tcs = new TaskCompletionSource<string>();
        await Navigation.PushAsync(new QrScanPage(tcs));

        var result = await tcs.Task;
        if (string.IsNullOrWhiteSpace(result)) return;

        var parsed = await IncomingStockAddPopupPage.ShowAsync(_sp, result.Trim());
        if (parsed is null) return;
        _vm.AddLine(parsed);
    }

    private async void OnEditLineTapped(object sender, EventArgs e)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not IncomingStockLine line)
            return;

        var result = await IncomingStockAddPopupPage.ShowAsync(_sp, editLine: line);
        if (result is null) return;

        line.Origin = result.origin ?? string.Empty;
        line.MaterialCode = result.materialCode ?? string.Empty;
        line.MaterialName = result.materialName ?? string.Empty;
        line.FurnaceNo = result.furnaceNo ?? string.Empty;
        line.CoilNo = result.coilNo ?? string.Empty;
        line.Spec = result.spec ?? string.Empty;
        line.Qty = result.qty;
        line.ProductionDate = result.productionDate ?? string.Empty;
    }
}
