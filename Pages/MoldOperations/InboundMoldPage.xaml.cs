using IndustrialControlMAUI.Services;
using IndustrialControlMAUI.ViewModels;

namespace IndustrialControlMAUI.Pages
{
    public partial class InboundMoldPage : ContentPage
    {
        private readonly InboundMoldViewModel _vm;

        public InboundMoldPage(InboundMoldViewModel vm)
        {
            InitializeComponent();
            BindingContext = _vm = vm;
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

        private void OnRowCheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (!e.Value) return; // 只在勾上时选中
            if (sender is CheckBox cb && cb.BindingContext is MoldScanRow row)
            {
                _vm.SelectedRow = row;  // 触发 CollectionView 的选中高亮
            }
        }

    }
}
