using IndustrialControlMAUI.ViewModels;

namespace IndustrialControlMAUI.Pages;
[QueryProperty(nameof(WorkOrderNo), "workOrderNo")]
public partial class OutboundMoldPage : ContentPage
{
    public readonly OutboundMoldViewModel _vm;
    public string? WorkOrderNo { get; set; }
    CancellationTokenSource? _lifecycleCts;
    public OutboundMoldPage(OutboundMoldViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _lifecycleCts = new CancellationTokenSource();
        if (BindingContext is OutboundMoldViewModel vm)
            vm.SetLifecycleToken(_lifecycleCts.Token);
        if (!string.IsNullOrWhiteSpace(WorkOrderNo))
        {
            await _vm.LoadAsync(WorkOrderNo);
        }
        ScanEntry?.Focus();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        try { _lifecycleCts?.Cancel(); } catch { }
        _lifecycleCts?.Dispose();
        _lifecycleCts = null;
    }

    bool _submitting = false;

    private async void ScanEntry_Completed(object? sender, EventArgs e)
    {
        if (_submitting) return;
        _submitting = true;
        try
        {
            if (_vm.ScanSubmitCommand.CanExecute(null))
                await _vm.ScanSubmitCommand.ExecuteAsync(null); // ★ await 异步命令
        }
        finally
        {
            _submitting = false;
            await Task.Delay(30);
            ScanEntry.Text = string.Empty;
            ScanEntry?.Focus(); // 连续扫描更顺手
        }
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
        ScanEntry.Text = result.Trim();

        // 同步到 ViewModel
        if (BindingContext is OutboundMoldViewModel vm)
        {
            if (_vm.ScanSubmitCommand.CanExecute(null))
                await _vm.ScanSubmitCommand.ExecuteAsync(null); // ★ await 异步命令

            // 清空并继续聚焦，方便下一次输入/扫码
            ScanEntry.Text = string.Empty;
            ScanEntry.Focus();
        }
    }
}
