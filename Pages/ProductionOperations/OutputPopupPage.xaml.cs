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

    /// <summary>
    /// 打开“投入物料”弹窗：
    /// - presetMaterial != null 时：物料已预设，弹窗里只让填数量/备注（下拉禁用）
    /// - presetMaterial == null 时：根据 materialInputList 让用户选择物料
    /// </summary>
    public static async Task<OutputPopupResult?> ShowAsync(
        IServiceProvider? sp,
        IEnumerable<TaskMaterialOutput> materialOutputList,
        TaskMaterialOutput? presetMaterial = null)
    {
        var tcs = new TaskCompletionSource<OutputPopupResult?>();

        // 1) 取得 ServiceProvider（参数优先，其次全局，最后直接 new）
        var provider = sp ?? Application.Current?.Handler?.MauiContext?.Services;

        OutputPopupViewModel vm =
            provider is not null
                ? ActivatorUtilities.CreateInstance<OutputPopupViewModel>(provider)
                : new OutputPopupViewModel();

        // 2) 初始化 VM & 结果回传
        vm.Init(materialOutputList ?? Enumerable.Empty<TaskMaterialOutput>(), presetMaterial);
        vm.SetResultTcs(tcs);

        // 3) 打开弹窗（建议 await，防止导航异常被吞）
        var page = new OutputPopupPage(vm);
        if (Application.Current?.MainPage?.Navigation is not null)
            await Application.Current.MainPage.Navigation.PushModalAsync(page);

        // 4) 返回等待结果（调用方 await）
        return await tcs.Task;
    }
}
