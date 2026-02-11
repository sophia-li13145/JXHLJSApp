using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JXHLJSApp.Models;
using JXHLJSApp.Pages;
using JXHLJSApp.Popups;
using JXHLJSApp.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Views;


namespace JXHLJSApp.ViewModels;
public partial class WorkProcessTaskDetailViewModel : ObservableObject, IQueryAttributable
{
    private readonly IWorkOrderApi _api;

    // 状态字典（值→名），用于将 auditStatus 映射为中文
    private readonly Dictionary<string, string> _auditMap = new();
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string headerTitle = "生产管理系统";
    [ObservableProperty] private bool isSuanxiWorkshop;
    [ObservableProperty] private bool isRechuliWorkshop;
    [ObservableProperty] private bool isLasiWorkshop;
    [ObservableProperty] private bool isDefaultWorkshop;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CanPauseResume))]
    [NotifyPropertyChangedFor(nameof(CanFinish))]
    [NotifyPropertyChangedFor(nameof(PauseResumeText))]
    [NotifyPropertyChangedFor(nameof(IsEditing))]
    private TaskRunState state = TaskRunState.NotStarted;
    // ★ 只有 Running（开工/复工后）为 true，其它状态为 false
    public bool IsEditing => State == TaskRunState.Running;

    public bool CanStart => !IsBusy && State == TaskRunState.NotStarted;
    public bool CanPauseResume => !IsBusy && (State == TaskRunState.Running || State == TaskRunState.Paused);
    public bool CanFinish => !IsBusy && State == TaskRunState.Running;

    public string PauseResumeText => State == TaskRunState.Running ? "暂停" : "复工";

    [ObservableProperty] private DetailTab activeTab = DetailTab.Input;

    [ObservableProperty] private WorkProcessTaskDetail? detail;

    [ObservableProperty] private string? currentUserName; // 进入页面时赋值实际登录人
    // 投料记录列表（表格2的数据源）
    /// <summary>执行 new 逻辑。</summary>
    public ObservableCollection<MaterialAuRecord> MaterialInputRecords { get; } = new();
    // —— 产出记录列表（表2数据源）
    /// <summary>执行 new 逻辑。</summary>
    public ObservableCollection<OutputAuRecord> OutputRecords { get; } = new();
    public ObservableCollection<StatusOption> ShiftOptions { get; } = new();

    private TaskMaterialInput? _selectedMaterialItem;
    public TaskMaterialInput? SelectedMaterialItem
    {
        get => _selectedMaterialItem;
        set => SetProperty(ref _selectedMaterialItem, value);
    }
    private TaskMaterialOutput? _selectedOutputItem;
    public TaskMaterialOutput? SelectedOutputItem
    {
        get => _selectedOutputItem;
        set => SetProperty(ref _selectedOutputItem, value);
    }
    public string? ReportQtyText
    {
        get => Detail?.taskReportedQty?.ToString("G29");   // 显示：避免 0 尾
        set
        {
            if (Detail == null) return;
            if (decimal.TryParse(value, out var d))
                Detail.taskReportedQty = d;
            else
                Detail.taskReportedQty = null;
            OnPropertyChanged();                // 刷新自身
            OnPropertyChanged(nameof(Detail));  // 若其他地方也用到了 Detail
        }
    }

    /// <summary>执行 WorkProcessTaskDetailViewModel 初始化逻辑。</summary>
    public WorkProcessTaskDetailViewModel(IWorkOrderApi api)
    {
        _api = api;
        HeaderTitle = Preferences.Get("WorkShopName", Preferences.Get("WorkshopName", "生产管理系统"));
        if (string.IsNullOrWhiteSpace(HeaderTitle)) HeaderTitle = "生产管理系统";
        ApplyWorkshopLayout();
    }

    private void ApplyWorkshopLayout()
    {
        var ws = HeaderTitle?.Trim() ?? string.Empty;
        IsSuanxiWorkshop = ws.Contains("酸洗", StringComparison.OrdinalIgnoreCase);
        IsRechuliWorkshop = ws.Contains("热处理", StringComparison.OrdinalIgnoreCase);
        IsLasiWorkshop = ws.Contains("拉丝", StringComparison.OrdinalIgnoreCase);
    }
    
   
    partial void OnIsBusyChanged(bool value) => NotifyAllCanExec();
    /// <summary>执行 OnStateChanged 逻辑。</summary>
    partial void OnStateChanged(TaskRunState value) => NotifyAllCanExec();
    /// <summary>执行 NotifyAllCanExec 逻辑。</summary>
    private void NotifyAllCanExec()
    {
        (StartWorkCommand as IRelayCommand)?.NotifyCanExecuteChanged();
        (PauseResumeCommand as IRelayCommand)?.NotifyCanExecuteChanged();
        (FinishCommand as IRelayCommand)?.NotifyCanExecuteChanged();
    }


    /// <summary>执行 ApplyQueryAttributes 逻辑。</summary>
    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var v) && v is string id && !string.IsNullOrWhiteSpace(id))
        {
            await InitAsync(id);
        }
    }
    /// <summary>执行 NotifyCanExec 逻辑。</summary>
    private void NotifyCanExec()
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanPauseResume));
        OnPropertyChanged(nameof(CanFinish));
    }

    /// <summary>执行 StartWorkAsync 逻辑。</summary>
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartWorkAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            var resp = await _api.StartWorkAsync(Detail.processCode, Detail.workOrderNo, null);
            if (resp.success && resp.result)
            {
                State = TaskRunState.Running;
                await Shell.Current.DisplayAlert("提示", "开工成功！", "确定");
            }
            else
            {
                await Shell.Current.DisplayAlert("错误", resp.message ?? "开工失败！", "确定");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("异常", ex.Message, "确定");
        }
        finally
        {
            IsBusy = false;
        }
    }


    /// <summary>执行 PauseResumeAsync 逻辑。</summary>
    [RelayCommand(CanExecute = nameof(CanPauseResume))]
    private async Task PauseResumeAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            if (State == TaskRunState.Running)
            {
                await LoadShiftsAsync();
                var popup = new PauseWorkPopup(ShiftOptions, Detail?.taskReportedQty);
                var popupResultObj = await Shell.Current.CurrentPage.ShowPopupAsync(popup);
                if (popupResultObj is not PauseWorkPopupResult popupResult || popupResult.IsCanceled)
                    return;

                var updateResp = await _api.UpdateWorkProcessTaskAsync(
                    id: Detail.id,
                    productionMachine: null,
                    productionMachineName: null,
                    taskReportedQty: popupResult.ExistingReportedQty + popupResult.ReportQty,
                    teamCode: popupResult.SelectedShiftCode,
                    teamName: popupResult.SelectedShiftName,
                    workHours: null,
                    startDate: null,
                    endDate: null,
                    ct: default);

                if (!updateResp.Succeeded)
                {
                    await Application.Current.MainPage.DisplayAlert("失败", updateResp.Message ?? "更新工序任务失败", "确定");
                    return;
                }

                var resp = await _api.PauseWorkAsync(Detail.processCode, Detail.workOrderNo, null, 0);
                if (resp.success && resp.result)
                {
                    if (Detail != null)
                    {
                        Detail.teamCode = popupResult.SelectedShiftCode;
                        Detail.taskReportedQty = popupResult.ExistingReportedQty + popupResult.ReportQty;
                        OnPropertyChanged(nameof(Detail));
                        OnPropertyChanged(nameof(ReportQtyText));
                    }

                    State = TaskRunState.Paused;
                    await Application.Current.MainPage.DisplayAlert("成功", "已暂停。", "确定");
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("失败", resp.message ?? "暂停失败", "确定");
                }
            }
            else if (State == TaskRunState.Paused)
            {
                // 当前为“已暂停”，点击 => 执行“恢复”
                bool go = await Application.Current.MainPage.DisplayAlert("确认", "确定恢复生产吗？", "恢复", "取消");
                if (!go) return;

                var resp = await _api.PauseWorkAsync(Detail.processCode, Detail.workOrderNo, null, 1);
                if (resp.success && resp.result)
                {
                    State = TaskRunState.Running;
                    await Application.Current.MainPage.DisplayAlert("成功", "已恢复生产。", "确定");
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("失败", resp.message ?? "恢复失败", "确定");
                }
            }
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("异常", ex.Message, "确定");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>执行 FinishAsync 逻辑。</summary>
    [RelayCommand(CanExecute = nameof(CanFinish))]
    private async Task FinishAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            var planQtyText = Detail?.scheQty?.ToString("G29") ?? "0";
            var input = await Application.Current.MainPage.DisplayPromptAsync(
                "完工确认",
                $"计划数量：{planQtyText}\n报工数量：",
                "确定",
                "取消",
                placeholder: "请输入报工数量",
                keyboard: Keyboard.Numeric);

            if (input is null) return;

            input = input.Trim();
            if (!int.TryParse(input, out var reportQty) || reportQty < 0)
            {
                await Shell.Current.DisplayAlert("提示", "报工数量格式不正确。", "确定");
                return;
            }

            var updateResp = await _api.UpdateWorkProcessTaskAsync(
                id: Detail.id,
                productionMachine: null,
                productionMachineName: null,
                taskReportedQty: reportQty,
                teamCode: null,
                teamName: null,
                workHours: null,
                startDate: null,
                endDate: null,
                ct: default);

            if (!updateResp.Succeeded)
            {
                await Shell.Current.DisplayAlert("错误", updateResp.Message ?? "更新报工数量失败！", "确定");
                return;
            }

            var resp = await _api.CompleteWorkAsync(Detail.processCode, Detail.workOrderNo, null, reportQty);
            if (resp.success && resp.result)
            {
                if (Detail != null)
                {
                    Detail.taskReportedQty = reportQty;
                    OnPropertyChanged(nameof(Detail));
                    OnPropertyChanged(nameof(ReportQtyText));
                }

                State = TaskRunState.Finished;
                await Shell.Current.DisplayAlert("提示", "完工成功！", "确定");
            }
            else
            {
                await Shell.Current.DisplayAlert("错误", resp.message ?? "完工失败！", "确定");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("异常", ex.Message, "确定");
        }
        finally
        {
            IsBusy = false;
        }
    }



    /// <summary>执行 InitAsync 逻辑。</summary>
    [RelayCommand]
    private async Task InitAsync(string id)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await LoadAuditDictAsync();
            await LoadDetailAsync(id);
            ActiveTab = DetailTab.Input; // 会同步设置 IsInputVisible/IsOutputVisible
            CurrentUserName = Preferences.Get("UserName", string.Empty).Split('@')[0]; // 进入页面时赋值实际登录人
        }
        finally { IsBusy = false; }
    }

    /// <summary>执行 LoadAuditDictAsync 逻辑。</summary>
    private async Task LoadAuditDictAsync()
    {
        // 你已有：/normalService/pda/pmsWorkOrder/getWorkProcessTaskDictList
        var dict = await _api.GetWorkProcessTaskDictListAsync();
        _auditMap.Clear();
        var audit = dict.result?.FirstOrDefault(x => string.Equals(x.field, "auditStatus", StringComparison.OrdinalIgnoreCase));
        if (audit?.dictItems != null)
        {
            foreach (var d in audit.dictItems)
            {
                if (!string.IsNullOrWhiteSpace(d.dictItemValue))
                    _auditMap[d.dictItemValue!] = d.dictItemName ?? d.dictItemValue!;
            }
        }
    }

    /// <summary>执行 LoadDetailAsync 逻辑。</summary>
    private async Task LoadDetailAsync(string id)
    {
        var resp = await _api.GetWorkProcessTaskDetailAsync(id);
        if (resp.success && resp.result != null)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Detail = resp.result;                 // 若 Detail 有 setter 里 OnPropertyChanged(); 更好
                OnPropertyChanged(nameof(Detail));    // 确保 Detail.* 绑定能刷新
                OnPropertyChanged(nameof(ReportQtyText)); // 让 Entry 立刻拿到最新文本
            });

            // 映射中文名
            if (!string.IsNullOrWhiteSpace(Detail.auditStatus) &&
                _auditMap.TryGetValue(Detail.auditStatus, out var s))
            {
                Detail.AuditStatusName = s;
            }
            var execRaw = resp.result.periodExecute;
            var exec = execRaw?.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(exec))
            {
                State = TaskRunState.NotStarted;     // ★ 关键：明确未开工
            }
            else
            {
                State = exec switch
                {
                    "working" => TaskRunState.Running,
                    "resume" => TaskRunState.Running,
                    "pause" => TaskRunState.Paused,
                    "complete" => TaskRunState.Finished,
                    _ => TaskRunState.NotStarted
                };

            }

           
        }
        else
        {
            // 可视化提示由页面处理
        }
    }


    /// <summary>执行 LoadShiftsAsync 逻辑。</summary>
    private async Task LoadShiftsAsync()
    {
        ShiftOptions.Clear();

        // 默认加一个“请选择”
        ShiftOptions.Add(new StatusOption { Text = "请选择", Value = null });

        var dictResp = await _api.GetWorkProcessTaskDictListAsync();
        var shiftDict = dictResp?.result?
            .FirstOrDefault(x => string.Equals(x.field, "dict_pms_shift_schedule", StringComparison.OrdinalIgnoreCase));

        foreach (var item in shiftDict?.dictItems ?? new())
        {
            ShiftOptions.Add(new StatusOption
            {
                Text = item.dictItemName ?? item.dictItemValue ?? string.Empty,
                Value = item.dictItemValue
            });
        }
    }

    /// <summary>执行 ShowTip 逻辑。</summary>
    private Task ShowTip(string message) =>
           Shell.Current?.DisplayAlert("提示", message, "确定") ?? Task.CompletedTask;

    /// <summary>执行 SubmitReportQtyAsync 逻辑。</summary>
    [RelayCommand]
    private async Task SubmitReportQtyAsync()
    {
        if (string.IsNullOrWhiteSpace(ReportQtyText))
        {
            await Shell.Current.DisplayAlert("提示", "请输入数量", "OK");
            return;
        }

        var qty = int.TryParse(ReportQtyText, out var value) ? value : 0;
        if (qty <= 0)
        {
            await Shell.Current.DisplayAlert("提示", "数量必须大于 0", "OK");
            return;
        }

        // 调用接口
        var r = await _api.UpdateWorkProcessTaskAsync(
            id: Detail.id, null, null, qty, null, null, null, null, null, default);

        if (!r.Succeeded)
            await ShowTip(string.IsNullOrWhiteSpace(r.Message) ? "更新报工数量失败" : r.Message);
        else
            await ShowTip("报工数量已更新");
    }



   
}
