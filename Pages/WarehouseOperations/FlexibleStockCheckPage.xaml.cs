using IndustrialControlMAUI.ViewModels;
using IndustrialControlMAUI.Services;
using System.Linq;

namespace IndustrialControlMAUI.Pages
{
    public partial class FlexibleStockCheckPage : ContentPage
    {
        private readonly FlexibleStockCheckViewModel _vm;

        public FlexibleStockCheckPage(FlexibleStockCheckViewModel vm)
        {
            InitializeComponent();
            BindingContext = _vm = vm;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // 首次进入，根据上一个页面传来的盘点单号 + 库位号自动加载列表
            await _vm.InitialLoadAsync();

            LocationEntry?.Focus();
        }

        // ====== Entry Completed 事件：回车查询 ======

        private void OnLocationCompleted(object sender, EventArgs e)
        {
            _vm.ScanLocationSubmitCommand.Execute(null);
        }

        private void OnMaterialCompleted(object sender, EventArgs e)
        {
            _vm.ScanMaterialSubmitCommand.Execute(null);
        }

        // ====== 扫码按钮 ======

        private async void OnLocationScanClicked(object sender, EventArgs e)
        {
            var tcs = new TaskCompletionSource<string>();
            await Navigation.PushAsync(new QrScanPage(tcs));

            var result = await tcs.Task;
            if (string.IsNullOrWhiteSpace(result))
                return;

            var code = result.Trim();
            LocationEntry.Text = code;
            _vm.LocationCode = code;

            await _vm.QueryDetailsAsync(_vm.LocationCode, _vm.MaterialBarcode);

            if (_vm.Details.Count == 1)
                _vm.OpenEditDialogCommand.Execute(_vm.Details[0]);

            LocationEntry.Focus();
        }

        private async void OnMaterialScanClicked(object sender, EventArgs e)
        {
            var tcs = new TaskCompletionSource<string>();
            await Navigation.PushAsync(new QrScanPage(tcs));

            var result = await tcs.Task;
            if (string.IsNullOrWhiteSpace(result))
                return;

            var code = result.Trim();
            MaterialEntry.Text = code;
            _vm.MaterialBarcode = code;

            await _vm.QueryDetailsAsync(_vm.LocationCode, _vm.MaterialBarcode);

            if (_vm.Details.Count == 1)
                _vm.OpenEditDialogCommand.Execute(_vm.Details[0]);

            MaterialEntry.Focus();
        }
    }
}
