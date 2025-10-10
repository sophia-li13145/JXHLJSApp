using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndustrialControlMAUI.Models;
using Kotlin;

namespace IndustrialControlMAUI.ViewModels
{
    public partial class MaterialInputPopupViewModel : ObservableObject
    {
        // 只读展示
        [ObservableProperty] private string materialName = "";
        [ObservableProperty] private string unit = "";

        // 输入
        [ObservableProperty] private string qtyText = "";
        [ObservableProperty] private string? memo;

        // 可能需要（如果你要传到接口：生产日期可选）
        [ObservableProperty] private DateTime? rawMaterialProductionDate;

        private MaterialInputItem? _selected;
        private TaskCompletionSource<MaterialInputPopupResult?>? _tcs;

        public void Setup(MaterialInputItem selected, TaskCompletionSource<MaterialInputPopupResult?> tcs)
        {
            _selected = selected;
            _tcs = tcs;
            MaterialName = selected.materialName;
            Unit = selected.unit;
        }

        [RelayCommand]
        private async Task ConfirmAsync()
        {
            // 数量支持负数（你示例有 -7）；仅禁止 0
            if (!double.TryParse(QtyText, out var qty) || qty == 0)
            {
                await Shell.Current.DisplayAlert("提示", "请输入非零的数量（可为负数）", "OK");
                return;
            }

            var result = new MaterialInputPopupResult
            {
                Qty = qty,
                Memo = string.IsNullOrWhiteSpace(Memo) ? null : Memo,
                RawMaterialProductionDate = RawMaterialProductionDate
            };

            _tcs?.TrySetResult(result);
            await Application.Current.MainPage.Navigation.PopModalAsync();
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            _tcs?.TrySetResult(null);
            await Application.Current.MainPage.Navigation.PopModalAsync();
        }
    }

}
