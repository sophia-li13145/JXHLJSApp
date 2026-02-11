using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JXHLJSApp.Models;
using System.Collections.ObjectModel;

namespace JXHLJSApp.Popups;

public class PauseWorkPopupResult
{
    public bool IsCanceled { get; init; }
    public string SelectedShiftCode { get; init; } = string.Empty;
    public string SelectedShiftName { get; init; } = string.Empty;
    public int ExistingReportedQty { get; init; }
    public int ReportQty { get; init; }
}

public partial class PauseWorkPopup : Popup
{
    public PauseWorkPopup(ObservableCollection<StatusOption> shiftOptions, decimal? existingReportedQty)
    {
        InitializeComponent();
        BindingContext = new Vm(shiftOptions, existingReportedQty, CloseWithResult);
    }

    private void CloseWithResult(PauseWorkPopupResult result) => Close(result);

    public partial class Vm : ObservableObject
    {
        private readonly Action<PauseWorkPopupResult> _close;

        public ObservableCollection<StatusOption> ShiftOptions { get; }

        [ObservableProperty] private StatusOption? selectedShift;
        [ObservableProperty] private string existingReportedQtyText = string.Empty;
        [ObservableProperty] private string reportQtyText = string.Empty;

        public Vm(
            ObservableCollection<StatusOption> shiftOptions,
            decimal? existingReportedQty,
            Action<PauseWorkPopupResult> close)
        {
            _close = close;
            ShiftOptions = shiftOptions;
            ExistingReportedQtyText = ((int)(existingReportedQty ?? 0)).ToString();
        }

        [RelayCommand]
        private void Cancel() => _close(new PauseWorkPopupResult { IsCanceled = true });

        [RelayCommand]
        private async Task Ok()
        {
            if (SelectedShift == null || string.IsNullOrWhiteSpace(SelectedShift.Value))
            {
                await Shell.Current.DisplayAlert("提示", "请选择班次。", "确定");
                return;
            }

            var existingQty = int.TryParse(ExistingReportedQtyText?.Trim(), out var parsedExistingQty) && parsedExistingQty >= 0
                ? parsedExistingQty
                : 0;

            if (!int.TryParse(ReportQtyText?.Trim(), out var reportQty) || reportQty <= 0)
            {
                await Shell.Current.DisplayAlert("提示", "请填写正确的报工数量。", "确定");
                return;
            }

            _close(new PauseWorkPopupResult
            {
                SelectedShiftCode = SelectedShift.Value,
                SelectedShiftName = SelectedShift.Text,
                ExistingReportedQty = existingQty,
                ReportQty = reportQty
            });
        }
    }
}
