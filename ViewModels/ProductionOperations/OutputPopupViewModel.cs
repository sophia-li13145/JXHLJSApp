using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndustrialControlMAUI.Models;
using Kotlin;


namespace IndustrialControlMAUI.ViewModels
{
    public partial class OutputPopupViewModel : ObservableObject
    {
        [ObservableProperty] private string materialName = "";
        [ObservableProperty] private string unit = "";
        [ObservableProperty] private string qtyText = "";
        [ObservableProperty] private string? memo;

        private TaskCompletionSource<OutputPopupResult?>? _tcs;

        public void Setup(OutputPlanItem selected, TaskCompletionSource<OutputPopupResult?> tcs)
        {
            MaterialName = selected.materialName;
            Unit = selected.unit;
            _tcs = tcs;
        }

        [RelayCommand]
        private async Task ConfirmAsync()
        {
            if (!double.TryParse(QtyText, out var qty) || qty == 0)
            {
                await Shell.Current.DisplayAlert("提示", "请输入非零的数量（可为负数）", "OK");
                return;
            }

            _tcs?.TrySetResult(new OutputPopupResult
            {
                Qty = qty,
                Memo = string.IsNullOrWhiteSpace(Memo) ? null : Memo
            });

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
