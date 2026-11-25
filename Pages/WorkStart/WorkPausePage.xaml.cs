using System.Globalization;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using JXHLJSApp.Services;
using JXHLJSApp.Services.WorkOrders;

namespace JXHLJSApp.Pages.WorkStart;

public partial class WorkPausePage : ContentPage
{
    private readonly IWorkOrderApi _workOrderApi;
    private readonly IProductionContextService _productionContext;
    private bool _isBusy;

    public WorkPausePage(IWorkOrderApi workOrderApi, IProductionContextService productionContext)
    {
        InitializeComponent();
        _workOrderApi = workOrderApi;
        _productionContext = productionContext;

        var workOrderNo = productionContext.Current?.WorkOrderNo;
        WorkOrderNoLabel.Text = string.IsNullOrWhiteSpace(workOrderNo) ? "--" : workOrderNo;
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnConfirmPauseClicked(object sender, EventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        var workOrderNo = _productionContext.Current?.WorkOrderNo;
        if (string.IsNullOrWhiteSpace(workOrderNo))
        {
            await DisplayAlert("提示", "当前生产工单为空，无法暂停生产工单。", "确定");
            return;
        }

        var lengthText = OutputLengthEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(lengthText))
        {
            await DisplayAlert("提示", "请输入产出长度数量。", "确定");
            return;
        }

        if (!decimal.TryParse(lengthText, NumberStyles.Number, CultureInfo.InvariantCulture, out var outputLength))
        {
            await DisplayAlert("提示", "产出长度数量格式不正确，请输入数字。", "确定");
            return;
        }

        if (outputLength < 0)
        {
            await DisplayAlert("提示", "产出长度数量不能小于 0。", "确定");
            return;
        }

        try
        {
            _isBusy = true;
            ConfirmButton.IsEnabled = false;

            // 后端仅要求工单号与产出长度必填，长度单位与备注不做采集。
            var result = await _workOrderApi.StopWorkOrderAsync(new WorkOrderStopRequestDto
            {
                workOrderNo = workOrderNo,
                outputLength = outputLength
            });

            if (!result.success)
            {
                var message = string.IsNullOrWhiteSpace(result.message)
                    ? "工单停工未成功，请稍后重试。"
                    : result.message;
                await ErrorDialogService.ShowAsync(this, "暂停失败", message, "确定");
                return;
            }

            UpdateProductionContextStatus("Paused");

            try
            {
                await Toast.Make("暂停成功", ToastDuration.Short).Show();
            }
            catch
            {
                // 轻提示失败不影响返回执行页。
            }

            // 带参数返回，让“生产执行中”页面立即重新拉取任务池与暂停记录。
            try
            {
                await Shell.Current.GoToAsync("..?pauseUpdated=true");
            }
            catch
            {
                await Shell.Current.GoToAsync("..");
            }
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "暂停失败", ex.Message, "确定");
        }
        finally
        {
            _isBusy = false;
            ConfirmButton.IsEnabled = true;
        }
    }

    private void UpdateProductionContextStatus(string status)
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
            OperationName = current.OperationName,
            ExecutionId = current.ExecutionId,
            MachineCode = current.MachineCode,
            IsPicklingMachineConfirmed = current.IsPicklingMachineConfirmed,
            Status = status,
            StartedAt = current.StartedAt,
            SessionId = current.SessionId
        });
    }
}
