using JXHLJSApp.Services;
using JXHLJSApp.Services.WorkOrders;

namespace JXHLJSApp.Pages.WorkStart;

public partial class WorkCompletionPage : ContentPage
{
    private readonly IWorkOrderApi _workOrderApi;
    private readonly IScanService _scanService;
    private readonly IProductionContextService _productionContext;
    private bool _isBusy;


    public WorkCompletionPage(
        IWorkOrderApi workOrderApi,
        IScanService scanService,
        IProductionContextService productionContext)
    {
        InitializeComponent();
        _workOrderApi = workOrderApi;
        _scanService = scanService;
        _productionContext = productionContext;
    }


    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnScanPanelTapped(object sender, TappedEventArgs e)
    {
        var code = await _scanService.ScanAsync("扫码机台二维码验证完工");
        if (!string.IsNullOrWhiteSpace(code))
        {
            await VerifyMachineAsync(code);
        }
    }

    private async void OnManualMachineCompleted(object sender, EventArgs e)
    {
        await VerifyMachineAsync(MachineCodeEntry.Text);
    }

    private async void OnManualMachineConfirmClicked(object sender, EventArgs e)
    {
        await VerifyMachineAsync(MachineCodeEntry.Text);
    }

    private async Task VerifyMachineAsync(string? machineCode)
    {
        if (_isBusy)
        {
            return;
        }

        var devCode = machineCode?.Trim();
        if (string.IsNullOrWhiteSpace(devCode))
        {
            await DisplayAlert("提示", "请扫描或输入机台二维码", "确定");
            return;
        }

        try
        {
            _isBusy = true;
            var result = await _workOrderApi.BindWorkerMachineAsync(devCode);
            if (!result)
            {
                await DisplayAlert("验证失败", "机台验证未成功，请确认机台二维码后重试。", "确定");
                return;
            }

            UpdateProductionContextMachine(devCode);
            await LoadCompletionStatusAsync();
            ScanPanel.IsVisible = false;
            ManualMachinePanel.IsVisible = false;
            SuccessBanner.IsVisible = true;
            ConfirmCard.IsVisible = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("验证失败", ex.Message, "确定");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task LoadCompletionStatusAsync()
    {
        var workOrderNo = _productionContext.Current?.WorkOrderNo;
        if (string.IsNullOrWhiteSpace(workOrderNo))
        {
            SystemStatusValueLabel.Text = "--";
            TargetStatusValueLabel.Text = "--";
            WeightProgressValueLabel.Text = "--";
            ConfirmButton.IsEnabled = false;
            return;
        }

        var status = await _workOrderApi.GetWorkOrderCompletionStatusAsync(workOrderNo);
        SystemStatusValueLabel.Text = status?.systemStatusDisplay ?? "--";
        TargetStatusValueLabel.Text = status?.targetStatusDisplay ?? "--";
        WeightProgressValueLabel.Text = status?.weightProgressDisplay ?? "--";
        ConfirmButton.IsEnabled = status?.isCompleted == true;
    }

    private void UpdateProductionContextMachine(string machineCode)
    {
        var current = _productionContext.Current;
        if (current is null)
        {
            return;
        }

        _productionContext.Set(new ProductionContext
        {
            WorkOrderId = current.WorkOrderId,
            WorkOrderNo = current.WorkOrderNo,
            ExecutionId = current.ExecutionId,
            MachineCode = machineCode,
            Status = current.Status,
            StartedAt = current.StartedAt,
            SessionId = current.SessionId
        });
    }

    private async void OnConfirmCompletionClicked(object sender, EventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        var workOrderNo = _productionContext.Current?.WorkOrderNo;
        if (string.IsNullOrWhiteSpace(workOrderNo))
        {
            await DisplayAlert("提示", "当前生产工单为空，无法确认完工。", "确定");
            return;
        }

        try
        {
            _isBusy = true;
            ConfirmButton.IsEnabled = false;
            var success = await _workOrderApi.ConfirmCompletionAsync(workOrderNo);
            if (!success)
            {
                await DisplayAlert("完工失败", "工单完工未成功，请稍后重试。", "确定");
                return;
            }

            _productionContext.Clear();
            await DisplayAlert("提示", "机台完工作业记录成功!", "确定");
            await Shell.Current.GoToAsync(AppShell.RouteHome);
        }
        catch (Exception ex)
        {
            await DisplayAlert("完工失败", ex.Message, "确定");
        }
        finally
        {
            ConfirmButton.IsEnabled = true;
            _isBusy = false;
        }
    }
}
