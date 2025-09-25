using IndustrialControlMAUI.Models;
using IndustrialControlMAUI.ViewModels;
using Microsoft.Maui.Controls;

namespace IndustrialControlMAUI.Pages;
[QueryProperty(nameof(WorkOrderNo), "workOrderNo")]
public partial class OutboundMoldPage : ContentPage
{
    public readonly OutboundMoldViewModel _vm;
    public string? WorkOrderNo { get; set; }
    public OutboundMoldPage(OutboundMoldViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync(WorkOrderNo ?? "");
        ScanEntry?.Focus();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
    }
}
