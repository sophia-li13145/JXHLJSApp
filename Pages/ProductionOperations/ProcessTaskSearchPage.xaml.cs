using IndustrialControlMAUI.Services;
using IndustrialControlMAUI.ViewModels;

namespace IndustrialControlMAUI.Pages;

public partial class ProcessTaskSearchPage : ContentPage
{
    private readonly ProcessTaskSearchViewModel _vm;

    public ProcessTaskSearchPage(ProcessTaskSearchViewModel vm, ScanService scanSvc)
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
