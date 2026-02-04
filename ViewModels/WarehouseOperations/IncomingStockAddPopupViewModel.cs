using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JXHLJSApp.Models;
using JXHLJSApp.Services;

namespace JXHLJSApp.ViewModels;

public partial class IncomingStockAddPopupViewModel : ObservableObject
{
    private readonly IIncomingStockService _api;
    private readonly IDialogService _dialogs;
    private TaskCompletionSource<IncomingBarcodeParseResult?>? _tcs;

    [ObservableProperty] private string barcode = string.Empty;
    [ObservableProperty] private string origin = string.Empty;
    [ObservableProperty] private string materialCode = string.Empty;
    [ObservableProperty] private string materialName = string.Empty;
    [ObservableProperty] private string furnaceNo = string.Empty;
    [ObservableProperty] private string coilNo = string.Empty;
    [ObservableProperty] private string spec = string.Empty;
    [ObservableProperty] private string productionDate = string.Empty;
    [ObservableProperty] private decimal? qty;
    [ObservableProperty] private bool isBusy;

    public IncomingStockAddPopupViewModel(IIncomingStockService api, IDialogService dialogs)
    {
        _api = api;
        _dialogs = dialogs;
    }

    public void SetResultTcs(TaskCompletionSource<IncomingBarcodeParseResult?> tcs) => _tcs = tcs;

    public async Task LoadByBarcodeAsync(string raw)
    {
        var barcodeValue = (raw ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(barcodeValue))
        {
            await _dialogs.AlertAsync("提示", "请先扫描或输入条码。");
            return;
        }

        Barcode = barcodeValue;
        IsBusy = true;
        try
        {
            var result = await _api.ParseIncomingBarcodeAsync(barcodeValue);
            if (result is null || result.success == false)
            {
                await _dialogs.AlertAsync("提示", "条码解析失败，请检查后重试。");
                return;
            }

            Barcode = result.barcode ?? barcodeValue;
            Origin = result.origin ?? string.Empty;
            MaterialCode = result.materialCode ?? string.Empty;
            MaterialName = result.materialName ?? string.Empty;
            FurnaceNo = result.furnaceNo ?? string.Empty;
            CoilNo = result.coilNo ?? string.Empty;
            Spec = result.spec ?? string.Empty;
            ProductionDate = result.productionDate ?? string.Empty;
            Qty = result.qty;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        var result = new IncomingBarcodeParseResult
        {
            barcode = Barcode,
            origin = Origin,
            materialCode = MaterialCode,
            materialName = MaterialName,
            furnaceNo = FurnaceNo,
            coilNo = CoilNo,
            spec = Spec,
            productionDate = ProductionDate,
            qty = Qty,
            success = true
        };

        _tcs?.TrySetResult(result);
        await CloseAsync();
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        _tcs?.TrySetResult(null);
        await CloseAsync();
    }

    private static Task CloseAsync()
        => Application.Current?.MainPage?.Navigation.PopModalAsync() ?? Task.CompletedTask;
}
