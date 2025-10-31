using IndustrialControlMAUI.ViewModels;

namespace IndustrialControlMAUI.Pages;

public partial class RepairDetailPage : ContentPage
{
    private readonly RepairDetailViewModel _vm;
    public RepairDetailPage() : this(ServiceHelper.GetService<RepairDetailViewModel>()) { }

    public RepairDetailPage(RepairDetailViewModel vm)
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