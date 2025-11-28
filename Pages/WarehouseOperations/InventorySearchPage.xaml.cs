using IndustrialControlMAUI.Services;
using IndustrialControlMAUI.ViewModels;

namespace IndustrialControlMAUI.Pages
{
    public partial class InventorySearchPage : ContentPage
    {
        private readonly InventorySearchViewModel _vm;
        private readonly IServiceProvider _sp;

        public InventorySearchPage(IServiceProvider sp, InventorySearchViewModel vm)
        {
            InitializeComponent();
            BindingContext = _vm = vm;
            _sp = sp;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            ScanEntry?.Focus();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
        }


        private void OnScanCompleted(object sender, EventArgs e)
        {
            _vm.ScanSubmitCommand.Execute(null);
        }

        private async void OnScanClicked(object sender, EventArgs e)
        {
            var tcs = new TaskCompletionSource<string>();
            await Navigation.PushAsync(new QrScanPage(tcs));

            // 等待扫码结果
            var result = await tcs.Task;
            if (string.IsNullOrWhiteSpace(result))
                return;

            // 回填到输入框
            ScanEntry.Text = result.Trim();

            // 同步到 ViewModel
            if (BindingContext is InventorySearchViewModel vm)
            {
                // 交给 VM 统一处理（第二个参数随意标记来源）
                await _vm.QueryInventoryAsync(ScanEntry.Text);

                // 清空并继续聚焦，方便下一次输入/扫码
                ScanEntry.Text = string.Empty;
                ScanEntry.Focus();
            }
        }
    }
}
