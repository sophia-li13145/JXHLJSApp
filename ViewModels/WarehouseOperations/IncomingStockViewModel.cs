using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JXHLJSApp.Models;
using JXHLJSApp.Services;

namespace JXHLJSApp.ViewModels;

public partial class IncomingStockViewModel : ObservableObject
{
    private readonly IIncomingStockService _api;
    private readonly IDialogService _dialogs;

    public ObservableCollection<IncomingStockLine> Lines { get; } = new();

    public IncomingStockViewModel(IIncomingStockService api, IDialogService dialogs)
    {
        _api = api;
        _dialogs = dialogs;
    }

    public async Task<bool> TryAddLineAsync(IncomingBarcodeParseResult parsed)
    {
        if (parsed is null) return false;

        var materialName = parsed.materialName?.Trim() ?? string.Empty;
        var materialCode = parsed.materialCode?.Trim() ?? string.Empty;
        var spec = parsed.spec?.Trim() ?? string.Empty;
        var identityKey = BuildIdentityKey(materialName, spec);
        if (string.IsNullOrWhiteSpace(identityKey)) return false;

        if (Lines.Any(x => string.Equals(BuildIdentityKey(x.MaterialName, x.Spec), identityKey, StringComparison.OrdinalIgnoreCase)))
        {
            await _dialogs.AlertAsync("提示", $"钢号 {materialName} / 规格 {spec} 已存在。");
            return false;
        }

        Lines.Add(new IncomingStockLine
        {
            Index = Lines.Count + 1,
            Barcode = parsed.barcode?.Trim() ?? identityKey,
            Origin = parsed.origin ?? string.Empty,
            MaterialCode = materialCode,
            MaterialName = materialName,
            FurnaceNo = parsed.furnaceNo ?? string.Empty,
            CoilNo = parsed.coilNo ?? string.Empty,
            Spec = spec,
            Qty = parsed.qty,
            ProductionDate = parsed.productionDate ?? string.Empty
        });

        return true;
    }

    public void ClearAll()
    {
        Lines.Clear();
    }

    public async Task<bool> SubmitPendingAsync()
    {
        if (Lines.Count == 0)
        {
            await _dialogs.AlertAsync("提示", "请先添加到货明细。");
            return false;
        }

        var payload = Lines.Select(x => new IncomingPendingStockRequest
        {
            coilNo = x.CoilNo,
            furnaceNo = x.FurnaceNo,
            materialCode = x.MaterialCode,
            materialName = string.IsNullOrWhiteSpace(x.MaterialName) ? null : x.MaterialName,
            origin = x.Origin,
            productionDate = x.ProductionDate,
            qty = x.Qty,
            spec = x.Spec
        }).ToArray();

        var ok = await _api.SubmitPendingStockAsync(payload);
        if (!ok.Succeeded)
        {
            await _dialogs.AlertAsync("提示", ok.Message ?? "提交失败，请稍后重试。");
        }
        return ok.Succeeded;
    }

    [RelayCommand]
    private async Task RemoveLineAsync(IncomingStockLine? line)
    {
        if (line is null) return;
        var identityKey = BuildIdentityKey(line.MaterialName, line.Spec);
        var confirm = await _dialogs.ConfirmAsync("确认", $"确定删除钢号 {line.MaterialName} / 规格 {line.Spec} 吗？");
        if (!confirm) return;

        Lines.Remove(line);
        Reindex();
    }

    private void Reindex()
    {
        for (var i = 0; i < Lines.Count; i++)
        {
            Lines[i].Index = i + 1;
        }
    }

    private static string BuildIdentityKey(string? materialCode, string? spec)
        => string.Join("-",
            (materialCode ?? string.Empty).Trim(),
            (spec ?? string.Empty).Trim());
}

public sealed class IncomingStockLine : ObservableObject
{
    private int _index;
    private string _barcode = string.Empty;
    private string _origin = string.Empty;
    private string _materialCode = string.Empty;
    private string _materialName = string.Empty;
    private string _furnaceNo = string.Empty;
    private string _coilNo = string.Empty;
    private string _spec = string.Empty;
    private decimal? _qty;
    private string _productionDate = string.Empty;

    public int Index
    {
        get => _index;
        set => SetProperty(ref _index, value);
    }

    public string Barcode
    {
        get => _barcode;
        set => SetProperty(ref _barcode, value);
    }

    public string Origin
    {
        get => _origin;
        set => SetProperty(ref _origin, value);
    }

    public string MaterialCode
    {
        get => _materialCode;
        set => SetProperty(ref _materialCode, value);
    }

    public string MaterialName
    {
        get => _materialName;
        set => SetProperty(ref _materialName, value);
    }

    public string FurnaceNo
    {
        get => _furnaceNo;
        set => SetProperty(ref _furnaceNo, value);
    }

    public string CoilNo
    {
        get => _coilNo;
        set => SetProperty(ref _coilNo, value);
    }

    public string Spec
    {
        get => _spec;
        set => SetProperty(ref _spec, value);
    }

    public decimal? Qty
    {
        get => _qty;
        set => SetProperty(ref _qty, value);
    }

    public string ProductionDate
    {
        get => _productionDate;
        set => SetProperty(ref _productionDate, value);
    }
}
