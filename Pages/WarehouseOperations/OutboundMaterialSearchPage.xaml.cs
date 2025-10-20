using IndustrialControlMAUI.Services;
using IndustrialControlMAUI.ViewModels;
namespace IndustrialControlMAUI.Pages;
public partial class OutboundMaterialSearchPage : ContentPage
{

    private readonly OutboundMaterialSearchViewModel _vm;
    public OutboundMaterialSearchPage(OutboundMaterialSearchViewModel vm)
    {
        _vm = vm;

        BindingContext = vm;
        InitializeComponent();

    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        OrderEntry.Focus();


    }

    /// <summary>
    /// 清空扫描记录
    /// </summary>
    void OnClearClicked(object sender, EventArgs e)
    {
        OrderEntry.Text = string.Empty;
        OrderEntry.Focus();
    }

    protected override void OnDisappearing()
    {

        base.OnDisappearing();
    }

    

    // 新增：扫码按钮事件
    private async void OnScanClicked(object sender, EventArgs e)
    {
        var tcs = new TaskCompletionSource<string>();
        await Navigation.PushAsync(new QrScanPage(tcs));

        // 等待扫码结果
        var result = await tcs.Task;
        if (string.IsNullOrWhiteSpace(result))
            return;

        // 回填到输入框
        OrderEntry.Text = result.Trim();

        // 同步到 ViewModel
        if (BindingContext is OutboundMaterialSearchViewModel vm)
        {
            vm.SearchOrderNo = result.Trim();

            // 可选：扫码后自动触发查询
            if (vm.SearchCommand.CanExecute(null))
                vm.SearchCommand.Execute(null);
        }
    }
}
