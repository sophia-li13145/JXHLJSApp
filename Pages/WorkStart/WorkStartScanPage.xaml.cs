namespace JXHLJSApp.Pages.WorkStart;

public partial class WorkStartScanPage : ContentPage
{
    public WorkStartScanPage()
    {
        InitializeComponent();
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnScanTapped(object sender, TappedEventArgs e)
    {
        await DisplayAlert("扫码上机", "请对准机台二维码进行扫描。", "确定");
    }

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        var machineNo = MachineNoEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(machineNo))
        {
            await DisplayAlert("提示", "请输入机台编号", "确定");
            return;
        }

        await DisplayAlert("机台编号", machineNo, "确定");
    }
}
