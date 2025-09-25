using IndustrialControlMAUI.Services;
using IndustrialControlMAUI.ViewModels;

namespace IndustrialControlMAUI.Pages;

public partial class OutboundMoldSearchPage : ContentPage
{
    private readonly OutboundMoldSearchViewModel _vm;

    public OutboundMoldSearchPage(OutboundMoldSearchViewModel vm, ScanService scanSvc)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        OrderEntry.Focus();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
    }


}
