using IndustrialControlMAUI.Models;
using IndustrialControlMAUI.ViewModels;

namespace IndustrialControlMAUI.Pages;

public partial class OutputPopupPage : ContentPage
{
    private readonly OutputPopupViewModel _vm;
    public OutputPopupPage(OutputPopupViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    public static Task<OutputPopupResult?> ShowAsync(IServiceProvider sp, OutputPlanItem selected)
    {
        var tcs = new TaskCompletionSource<OutputPopupResult?>();
        var vm = ActivatorUtilities.CreateInstance<OutputPopupViewModel>(sp);
        vm.Setup(selected, tcs);

        var page = new OutputPopupPage(vm);
        Application.Current.MainPage.Navigation.PushModalAsync(page);
        return tcs.Task;
    }

}
