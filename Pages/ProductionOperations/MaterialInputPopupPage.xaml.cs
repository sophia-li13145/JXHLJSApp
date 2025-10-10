using IndustrialControlMAUI.Models;
using IndustrialControlMAUI.ViewModels;

namespace IndustrialControlMAUI.Pages;

public partial class MaterialInputPopupPage : ContentPage
{
    private readonly MaterialInputPopupViewModel _vm;

    public MaterialInputPopupPage(MaterialInputPopupViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // 新签名：只接收已选中的那一条
    public static Task<MaterialInputPopupResult?> ShowAsync(IServiceProvider sp, MaterialInputItem selected)
    {
        var tcs = new TaskCompletionSource<MaterialInputPopupResult?>();
        var vm = ActivatorUtilities.CreateInstance<MaterialInputPopupViewModel>(sp);
        vm.Setup(selected, tcs);

        var page = new MaterialInputPopupPage(vm);
        Application.Current.MainPage.Navigation.PushModalAsync(page);

        return tcs.Task;
    }
}

