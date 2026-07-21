using JXHLJSApp.Services;
using JXHLJSApp.Models;
using JXHLJSApp.Services.Transport;
using System.Globalization;

namespace JXHLJSApp.Pages.Transport;

public partial class ProcessTransferConfirmPage : ContentPage
{
    private readonly ITransportOrderApi _transportOrderApi;
    private TransportOrderDto? _order;
    private bool _busy;

    public ProcessTransferConfirmPage(ITransportOrderApi transportOrderApi)
    {
        _transportOrderApi = transportOrderApi;
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _order = TransportOrderNavigationStore.Current;
        BindOrder();
    }

    private void BindOrder()
    {
        MaterialNameLabel.Text = FirstNonEmpty(_order?.materialName, _order?.materialCode, "--");
        CurrentProcessLabel.Text = FirstNonEmpty(_order?.currentProcess, "--");
        NextProcessLabel.Text = $"👉 {FirstNonEmpty(_order?.nextProcess, "--")}";
        TotalQuantityLabel.Text = FormatQuantity(_order?.totalQuantity ?? _order?.quantity, _order?.unit);
        TotalWeightLabel.Text = FormatWeight(_order?.totalWeight ?? _order?.weight);
        WorkOrderNoLabel.Text = FirstNonEmpty(_order?.workOrderNo, _order?.transportOrderNo, "--");
    }

    private async void OnCancelTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        if (_busy) return;
        var transportOrderNo = _order?.transportOrderNo?.Trim();
        if (string.IsNullOrWhiteSpace(transportOrderNo))
        {
            await DisplayAlert("提示", "缺少运输单号，无法完成转运。", "确定");
            return;
        }

        try
        {
            _busy = true;
            var success = await _transportOrderApi.CompleteTransportOrderAsync(transportOrderNo);
            if (!success)
            {
                await ErrorDialogService.ShowAsync(this, "完成转运失败", "接口未确认转运完成，请稍后重试。", "确定");
                return;
            }

            await Shell.Current.GoToAsync(AppShell.RouteProcessTransferSuccess);
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "完成转运失败", ex.Message, "确定");
        }
        finally
        {
            _busy = false;
        }
    }

    private static string FormatQuantity(decimal? value, string? unit) => value.HasValue ? $"{value.Value.ToString("N0", CultureInfo.InvariantCulture)} {FirstNonEmpty(unit, "件")}" : "--";
    private static string FormatWeight(decimal? value) => value.HasValue ? $"{value.Value.ToString("N0", CultureInfo.InvariantCulture)} kg" : "--";
    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
