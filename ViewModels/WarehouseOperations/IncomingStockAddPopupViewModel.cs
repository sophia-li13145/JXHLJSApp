using System.Globalization;
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
    [ObservableProperty] private bool isEditMode;
    [ObservableProperty] private string confirmButtonText = "确认";
    [ObservableProperty] private bool isBarcodeReadOnly;
    [ObservableProperty] private string barcodeBackgroundColor = "White";

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
        SetEditMode(false);
        IsBusy = true;
        try
        {
            var response = await _api.ParseIncomingBarcodeAsync(barcodeValue);
            var result = response?.result;
            if (response?.success != true || result is null)
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

    public void LoadForEdit(IncomingStockLine line)
    {
        if (line is null) return;

        SetEditMode(true);
        Barcode = line.Barcode ?? string.Empty;
        Origin = line.Origin ?? string.Empty;
        MaterialCode = line.MaterialCode ?? string.Empty;
        MaterialName = line.MaterialName ?? string.Empty;
        FurnaceNo = line.FurnaceNo ?? string.Empty;
        CoilNo = line.CoilNo ?? string.Empty;
        Spec = line.Spec ?? string.Empty;
        ProductionDate = line.ProductionDate ?? string.Empty;
        Qty = line.Qty;
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        var validationMessage = ValidateRequiredFields();
        if (validationMessage is not null)
        {
            await _dialogs.AlertAsync("提示", validationMessage);
            return;
        }

        var composedBarcode = BuildCompositeBarcode();

        if (!IsEditMode)
        {
            IsBusy = true;
            try
            {
                var response = await _api.ParseIncomingBarcodeAsync(composedBarcode);
                if (response?.success != true || response.result is null)
                {
                    await _dialogs.AlertAsync("提示", response?.message ?? "条码解析失败，请检查后重试。");
                    return;
                }

                var parsed = response.result;
                parsed.barcode ??= composedBarcode;
                _tcs?.TrySetResult(parsed);
                await CloseAsync();
                return;
            }
            finally
            {
                IsBusy = false;
            }
        }

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

    private void SetEditMode(bool enabled)
    {
        IsEditMode = enabled;
        ConfirmButtonText = enabled ? "确认修改" : "确认";
        IsBarcodeReadOnly = enabled;
        BarcodeBackgroundColor = enabled ? "#E0E0E0" : "White";
    }

    private string BuildCompositeBarcode()
    {
        var qtyText = Qty?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        return string.Join("-",
            (Origin ?? string.Empty).Trim(),
            (MaterialCode ?? string.Empty).Trim(),
            (FurnaceNo ?? string.Empty).Trim(),
            (CoilNo ?? string.Empty).Trim(),
            qtyText.Trim(),
            (Spec ?? string.Empty).Trim(),
            (ProductionDate ?? string.Empty).Trim());
    }

    private string? ValidateRequiredFields()
    {
        if (string.IsNullOrWhiteSpace(Barcode)) return "请填写条码。";
        if (string.IsNullOrWhiteSpace(Origin)) return "请填写产地。";
        if (string.IsNullOrWhiteSpace(MaterialCode)) return "请填写钢号。";
        if (string.IsNullOrWhiteSpace(FurnaceNo)) return "请填写炉号。";
        if (string.IsNullOrWhiteSpace(MaterialName)) return "请填写牌号。";
        if (!Qty.HasValue) return "请填写重量。";
        if (string.IsNullOrWhiteSpace(Spec)) return "请填写规格。";
        if (string.IsNullOrWhiteSpace(ProductionDate)) return "请填写生产日期。";
        if (string.IsNullOrWhiteSpace(CoilNo)) return "请填写卷号。";
        return null;
    }
}
