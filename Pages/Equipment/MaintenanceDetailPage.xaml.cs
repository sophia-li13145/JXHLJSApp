using IndustrialControlMAUI.ViewModels;

namespace IndustrialControlMAUI.Pages;

public partial class MaintenanceDetailPage : ContentPage
{
    private readonly MaintenanceDetailViewModel _vm;
    public MaintenanceDetailPage() : this(ServiceHelper.GetService<MaintenanceDetailViewModel>()) { }

    public MaintenanceDetailPage(MaintenanceDetailViewModel vm)
    {
        InitializeComponent();
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
        BindingContext = _vm;
    }

   
    protected override void OnAppearing()
    {
        base.OnAppearing();


    }

    

}