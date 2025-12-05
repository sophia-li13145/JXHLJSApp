using IndustrialControlMAUI.ViewModels;

namespace IndustrialControlMAUI.Pages;

public partial class StockCheckSearchPage : ContentPage
{
    private readonly StockCheckSearchViewModel _vm;

    public StockCheckSearchPage(StockCheckSearchViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    private async void OnScanClicked(object sender, EventArgs e)
    {
        var tcs = new TaskCompletionSource<string>();
        await Navigation.PushAsync(new QrScanPage(tcs));

        var result = await tcs.Task;
        if (string.IsNullOrWhiteSpace(result))
            return;

        // 把扫码结果写入查询条件
        _vm.SearchCheckNo = result.Trim();

        // 重新按当前条件查第一页 10 条
        await _vm.SearchAsync();

        // 清空输入框并聚焦
        OrderEntry.Text = string.Empty;
        OrderEntry.Focus();
    }



    protected override async void OnAppearing()
    {
        base.OnAppearing();
        OrderEntry.Focus();
        await _vm.SearchAsync();

    }
}
