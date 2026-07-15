using System.ComponentModel;
using JXHLJSApp.Models.Warehouse;

namespace JXHLJSApp.Pages.Warehouse;

/// <summary>
/// 原料/半成品动态字段、重量单位和提交逻辑。
/// 本文件与 AddRawMaterialReceivingPage.xaml.cs 属于同一个 partial class，
/// 无需修改 csproj，放进 Pages/Warehouse 目录即可自动参与编译。
/// </summary>
public partial class AddRawMaterialReceivingPage
{
    private void OnTicketMaterialClassChangedV2(object sender, EventArgs e)
    {
        RefreshMaterialClassFormVisibilityV2(isBindDialog: false);
    }

    private void OnBindMaterialClassChangedV2(object sender, EventArgs e)
    {
        RefreshMaterialClassFormVisibilityV2(isBindDialog: true);
    }

    private void OnTicketOverlayPropertyChangedV2(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "IsVisible" && TicketConfirmOverlay.IsVisible)
        {
            ApplyPendingOcrDefaultsV2();
            RefreshMaterialClassFormVisibilityV2(isBindDialog: false);
        }
    }

    private void OnBindOverlayPropertyChangedV2(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "IsVisible")
        {
            return;
        }

        WarehousePicker.IsEnabled = !BindConfirmOverlay.IsVisible;

        if (BindConfirmOverlay.IsVisible)
        {
            RefreshMaterialClassFormVisibilityV2(isBindDialog: true);
        }
    }

    private void ApplyPendingOcrDefaultsV2()
    {
        if (_pendingOcr is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(MaterialNameEntry.Text))
        {
            MaterialNameEntry.Text =
                !string.IsNullOrWhiteSpace(_pendingOcr.materialName)
                    ? _pendingOcr.materialName
                    : _pendingOcr.productName;
        }

        var candidates = new[]
        {
            _pendingOcr.materialClass,
            _pendingOcr.materialClassName,
            _pendingOcr.materialType
        }
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .ToList();

        if (candidates.Count == 0)
        {
            return;
        }

        var option = _materialClassOptions.FirstOrDefault(item =>
            candidates.Any(candidate =>
                string.Equals(
                    item.dictItemValue,
                    candidate,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    item.dictItemName,
                    candidate,
                    StringComparison.OrdinalIgnoreCase)));

        if (option is null)
        {
            var ocrSaysSemiFinished =
                candidates.Any(ContainsSemiFinishedTextV2);

            var ocrSaysRaw =
                candidates.Any(ContainsRawMaterialTextV2);

            option = _materialClassOptions.FirstOrDefault(item =>
                (ocrSaysSemiFinished &&
                 IsSemiFinishedV2(item)) ||
                (ocrSaysRaw &&
                 IsRawMaterialV2(item)));
        }

        if (option is not null &&
            !ReferenceEquals(
                MaterialTypePicker.SelectedItem,
                option))
        {
            MaterialTypePicker.SelectedItem = option;
        }
    }

    private void RefreshMaterialClassFormVisibilityV2(bool isBindDialog)
    {
        ApplyMaterialClassFormVisibilityV2(isBindDialog);

        // 原文件中仍有一次 50ms 的旧刷新逻辑。
        // 这里延迟 120ms 再应用一次，确保最终显示状态以新逻辑为准。
        Dispatcher.DispatchDelayed(
            TimeSpan.FromMilliseconds(120),
            () => ApplyMaterialClassFormVisibilityV2(isBindDialog));
    }

    private void ApplyMaterialClassFormVisibilityV2(bool isBindDialog)
    {
        var picker = isBindDialog
            ? BindMaterialTypePicker
            : MaterialTypePicker;

        var materialClass = GetSelectedMaterialClassV2(picker);
        var isSemiFinished = IsSemiFinishedV2(materialClass);

        if (isBindDialog)
        {
            BindSemiFieldsRow1.IsVisible = isSemiFinished;
            BindSemiFieldsRow2.IsVisible = isSemiFinished;
            BindPieceWeightLabel.Text = isSemiFinished
                ? "实际件重（KG） *"
                : "实际件重（吨） *";
            return;
        }

        TicketSemiFieldsRow1.IsVisible = isSemiFinished;
        TicketSemiFieldsRow2.IsVisible = isSemiFinished;
        TicketPieceWeightLabel.Text = isSemiFinished
            ? "件重（KG） *"
            : "件重（吨） *";
    }

    private DictItemDto? GetSelectedMaterialClassV2(
        Picker picker,
        string? fallbackValue = null)
    {
        if (picker.SelectedItem is DictItemDto selected)
        {
            return selected;
        }

        if (picker.SelectedIndex >= 0 &&
            picker.SelectedIndex < _materialClassOptions.Count)
        {
            return _materialClassOptions[picker.SelectedIndex];
        }

        if (!string.IsNullOrWhiteSpace(fallbackValue))
        {
            var fallback = _materialClassOptions.FirstOrDefault(option =>
                string.Equals(
                    option.dictItemValue,
                    fallbackValue,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    option.dictItemName,
                    fallbackValue,
                    StringComparison.OrdinalIgnoreCase));

            if (fallback is not null)
            {
                return fallback;
            }
        }

        return _materialClassOptions.FirstOrDefault();
    }

    private static bool IsSemiFinishedV2(DictItemDto? materialClass)
    {
        return ContainsSemiFinishedTextV2(materialClass?.dictItemName) ||
               ContainsSemiFinishedTextV2(materialClass?.dictItemValue);
    }

    private static bool IsSemiFinishedV2(RawMaterialOcrDto item)
    {
        return ContainsSemiFinishedTextV2(item.materialClassName) ||
               ContainsSemiFinishedTextV2(item.materialClass) ||
               ContainsSemiFinishedTextV2(item.materialType);
    }

    private static bool ContainsSemiFinishedTextV2(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains(
                   "半成品",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Contains(
                   "semi",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsRawMaterialTextV2(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains(
                   "原料",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Contains(
                   "raw",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRawMaterialV2(DictItemDto? materialClass)
    {
        return ContainsRawMaterialTextV2(materialClass?.dictItemName) ||
               ContainsRawMaterialTextV2(materialClass?.dictItemValue);
    }

    private static string? ResolveMaterialClassValueV2(
        DictItemDto materialClass)
    {
        return !string.IsNullOrWhiteSpace(
            materialClass.dictItemValue)
                ? materialClass.dictItemValue
                : materialClass.dictItemName;
    }

    private static string? ResolveMaterialClassNameV2(
        DictItemDto materialClass)
    {
        return !string.IsNullOrWhiteSpace(
            materialClass.dictItemName)
                ? materialClass.dictItemName
                : materialClass.dictItemValue;
    }

    private static string ResolvePieceWeightUnitV2(bool isSemiFinished)
    {
        return isSemiFinished ? "KG" : "吨";
    }

    private static string ResolvePieceWeightUnitV2(RawMaterialOcrDto item)
    {
        if (!string.IsNullOrWhiteSpace(item.pieceWeightUnit))
        {
            return item.pieceWeightUnit.Trim().Equals(
                "kg",
                StringComparison.OrdinalIgnoreCase)
                    ? "KG"
                    : item.pieceWeightUnit.Trim();
        }

        return ResolvePieceWeightUnitV2(IsSemiFinishedV2(item));
    }

    private static decimal ParsePieceWeightValueV2(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0m;
        }

        var cleaned = value
            .Trim()
            .Replace("吨", string.Empty)
            .Replace("KG", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        return ParseNullableDecimal(cleaned) ?? 0m;
    }

    private static decimal ParsePieceWeightInTonsV2(RawMaterialOcrDto item)
    {
        var value = ParsePieceWeightValueV2(item.pieceWeight);
        var isKg =
            ResolvePieceWeightUnitV2(item).Equals(
                "KG",
                StringComparison.OrdinalIgnoreCase) ||
            (item.pieceWeight?.Contains(
                "KG",
                StringComparison.OrdinalIgnoreCase) ?? false);

        return isKg ? value / 1000m : value;
    }

    private async void OnConfirmTicketV2Clicked(
        object sender,
        EventArgs e)
    {
        var materialClass = GetSelectedMaterialClassV2(
            MaterialTypePicker,
            _pendingOcr?.materialClass);

        if (materialClass is null)
        {
            await DisplayAlert(
                "提示",
                "请选择物料分类。",
                "确定");
            return;
        }

        var pieceWeight = ParsePieceWeightValueV2(
            PieceWeightEntry.Text);

        if (pieceWeight <= 0m)
        {
            await DisplayAlert(
                "提示",
                "请输入有效的件重。",
                "确定");
            return;
        }

        var isSemiFinished = IsSemiFinishedV2(materialClass);

        _selectedTicket = new RawMaterialOcrDto
        {
            qrCode = _pendingOcr?.qrCode,
            coilNo = _pendingOcr?.coilNo,
            companyName = _pendingOcr?.companyName,
            confidence = _pendingOcr?.confidence,
            productName = _pendingOcr?.productName,
            productionDate = _pendingOcr?.productionDate,
            standard = _pendingOcr?.standard,
            workshop = _pendingOcr?.workshop,

            materialClass =
                ResolveMaterialClassValueV2(materialClass),
            materialClassName =
                ResolveMaterialClassNameV2(materialClass),
            materialType =
                ResolveMaterialClassValueV2(materialClass),
            materialCode = MaterialCodeEntry.Text?.Trim(),
            materialName = MaterialNameEntry.Text?.Trim(),
            spec = SpecEntry.Text?.Trim(),
            furnaceNo = FurnaceNoEntry.Text?.Trim(),
            originPlace = OriginPlaceEntry.Text?.Trim(),
            pieceWeight = pieceWeight.ToString(
                "0.############################",
                System.Globalization.CultureInfo.InvariantCulture),
            pieceWeightUnit = ResolvePieceWeightUnitV2(
                isSemiFinished),

            // 原料不提交半成品字段，避免用户切换分类后旧值被带入。
            strength = isSemiFinished
                ? StrengthEntry.Text?.Trim()
                : null,
            coilCount = isSemiFinished
                ? CoilCountEntry.Text?.Trim()
                : null,
            coilDiameter = isSemiFinished
                ? CoilDiameterEntry.Text?.Trim()
                : null,

            ocrRawText = _pendingOcr?.ocrRawText
        };

        try
        {
            await SaveOcrIncomingImageAsync(_selectedTicket);
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "保存票签失败",
                ex.Message,
                "确定");
            return;
        }

        ApplySelectedTicket(_selectedTicket);
        TicketConfirmOverlay.IsVisible = false;
    }

    private async void OnConfirmBindV2Clicked(
        object sender,
        EventArgs e)
    {
        var materialClass = GetSelectedMaterialClassV2(
            BindMaterialTypePicker,
            _selectedTicket?.materialClass);

        if (materialClass is null)
        {
            await DisplayAlert(
                "提示",
                "请选择物料分类。",
                "确定");
            return;
        }

        var pieceWeight = ParsePieceWeightValueV2(
            BindPieceWeightEntry.Text);

        if (pieceWeight <= 0m)
        {
            await DisplayAlert(
                "提示",
                "请输入有效的实际件重。",
                "确定");
            return;
        }

        var isSemiFinished = IsSemiFinishedV2(materialClass);

        var bound = new RawMaterialOcrDto
        {
            qrCode = _pendingQrCode,
            materialClass =
                ResolveMaterialClassValueV2(materialClass),
            materialClassName =
                ResolveMaterialClassNameV2(materialClass),
            materialType =
                ResolveMaterialClassValueV2(materialClass),
            materialCode = BindMaterialCodeEntry.Text?.Trim(),
            materialName = BindMaterialNameEntry.Text?.Trim(),
            spec = BindSpecEntry.Text?.Trim(),
            furnaceNo = BindFurnaceNoEntry.Text?.Trim(),
            originPlace = BindOriginPlaceEntry.Text?.Trim(),
            pieceWeight = pieceWeight.ToString(
                "0.############################",
                System.Globalization.CultureInfo.InvariantCulture),
            pieceWeightUnit = ResolvePieceWeightUnitV2(
                isSemiFinished),

            strength = isSemiFinished
                ? BindStrengthEntry.Text?.Trim()
                : null,
            coilCount = isSemiFinished
                ? BindCoilCountEntry.Text?.Trim()
                : null,
            coilDiameter = isSemiFinished
                ? BindCoilDiameterEntry.Text?.Trim()
                : null,

            ocrRawText = _selectedTicket?.ocrRawText
        };

        _ocrItems.Add(bound);
        MaterialListTitle.Text =
            $"待入库列表 ({_ocrItems.Count})";
        BindConfirmOverlay.IsVisible = false;
    }

    private async void OnCalculateSummaryV2Clicked(
        object sender,
        EventArgs e)
    {
        _summaryItems.Clear();

        var summaries = _ocrItems
            .GroupBy(item =>
                string.IsNullOrWhiteSpace(item.materialName)
                    ? "--"
                    : item.materialName.Trim())
            .Select(group =>
            {
                var first = group.First();
                var totalWeightInTons =
                    group.Sum(ParsePieceWeightInTonsV2);

                var materialType =
                    !string.IsNullOrWhiteSpace(
                        first.materialClassName)
                        ? first.materialClassName.Trim()
                        : IsSemiFinishedV2(first)
                            ? "半成品"
                            : "原料";

                return new MaterialSummaryItem(
                    group.Key,
                    materialType,
                    string.IsNullOrWhiteSpace(
                        first.originPlace)
                        ? "--"
                        : first.originPlace.Trim(),
                    group.Count(),
                    totalWeightInTons);
            })
            .OrderBy(item => item.materialName)
            .ToList();

        foreach (var summary in summaries)
        {
            _summaryItems.Add(summary);
        }

        if (_summaryItems.Count == 0)
        {
            await DisplayAlert(
                "提示",
                "暂无待入库物料可汇总。",
                "确定");
            return;
        }

        SummaryOverlay.IsVisible = true;
    }

    private async void OnSubmitInstockV2Clicked(
        object sender,
        EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_instockNo))
        {
            await DisplayAlert(
                "提示",
                "入库单号尚未生成，请稍后重试。",
                "确定");
            return;
        }

        var warehouse = GetSelectedWarehouse();
        if (warehouse is null ||
            string.IsNullOrWhiteSpace(
                warehouse.selectedName) ||
            string.IsNullOrWhiteSpace(
                warehouse.selectedCode))
        {
            await DisplayAlert(
                "提示",
                "请选择有效的入库仓库。",
                "确定");
            return;
        }

        if (_ocrItems.Count == 0)
        {
            await DisplayAlert(
                "提示",
                "请先扫码绑定至少一条待入库物料。",
                "确定");
            return;
        }

        var invalidItem = _ocrItems.FirstOrDefault(item =>
            string.IsNullOrWhiteSpace(item.qrCode) ||
            string.IsNullOrWhiteSpace(item.materialClass) ||
            ParsePieceWeightValueV2(item.pieceWeight) <= 0m);

        if (invalidItem is not null)
        {
            await DisplayAlert(
                "提示",
                "请确认每条明细都已填写追溯码、物料分类和有效件重。",
                "确定");
            return;
        }

        try
        {
            var request = new QuickInstockRequestDto
            {
                detailList = _ocrItems
                    .Select((item, index) =>
                    {
                        var isSemiFinished =
                            IsSemiFinishedV2(item);

                        return new QuickInstockDetailDto
                        {
                            coilCount = isSemiFinished
                                ? ParseNullableInt(item.coilCount)
                                : null,
                            coilDiameter = isSemiFinished
                                ? ParseNullableDecimal(
                                    item.coilDiameter)
                                : null,
                            count = 1,
                            countSeq = index + 1,
                            furnaceNo = item.furnaceNo,
                            instockNo = _instockNo,

                            // 数值保持用户输入值，单位单独传递。
                            // 例如 2.1 KG -> instockQty=2.1、unit=KG。
                            instockQty =
                                ParsePieceWeightValueV2(
                                    item.pieceWeight),

                            instockWarehouse =
                                warehouse.selectedName,
                            instockWarehouseCode =
                                warehouse.selectedCode,
                            materialClass =
                                item.materialClass,
                            materialCode =
                                item.materialCode,
                            materialName =
                                item.materialName,
                            origin =
                                item.originPlace,
                            qrCode =
                                item.qrCode,
                            spec =
                                item.spec,
                            strength = isSemiFinished
                                ? item.strength
                                : null,
                            unit =
                                ResolvePieceWeightUnitV2(item)
                        };
                    })
                    .ToList()
            };

            var success =
                await _warehouseApi.QuickInstockAsync(
                    request);

            if (success is false)
            {
                await DisplayAlert(
                    "提交失败",
                    "接口返回失败，请稍后重试。",
                    "确定");
                return;
            }

            await DisplayAlert(
                "提交成功",
                "采购入库已提交。",
                "确定");

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "提交失败",
                ex.Message,
                "确定");
        }
    }
}
