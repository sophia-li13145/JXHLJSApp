using Android.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndustrialControlMAUI.Models;
using IndustrialControlMAUI.Pages;
using IndustrialControlMAUI.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;


namespace IndustrialControlMAUI.ViewModels;
public partial class WorkProcessTaskDetailViewModel : ObservableObject, IQueryAttributable
{
    private readonly IWorkOrderApi _api;

    // 状态字典（值→名），用于将 auditStatus 映射为中文
    private readonly Dictionary<string, string> _auditMap = new();

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isEditing; // 点“开工”后 true
    [ObservableProperty] private bool isPaused;

    public bool CanStart => !IsBusy && !IsEditing;             // 未开工时可开工
    public bool CanPauseResume => !IsBusy && IsEditing;              // 开工后可暂停/复工
    public bool CanFinish => !IsBusy && IsEditing && !IsPaused; // 复工中才允许完工

    private readonly IServiceProvider _sp;
    public string PauseResumeText => IsPaused ? "复工" : "暂停";

    [ObservableProperty] private DetailTab activeTab = DetailTab.Input;
    [ObservableProperty] private bool isInputVisible = true;   // 默认显示投料
    [ObservableProperty] private bool isOutputVisible = false; // 默认隐藏产出

    public bool IsInputTab => ActiveTab == DetailTab.Input;
    public bool IsOutputTab => ActiveTab == DetailTab.Output;


    [ObservableProperty] private WorkProcessTaskDetail? detail;

    public ObservableCollection<TaskMaterialInput> Inputs { get; } = new();
    public ObservableCollection<TaskMaterialOutput> Outputs { get; } = new();

    // 班次/设备下拉
    public ObservableCollection<StatusOption> ShiftOptions { get; } = new();
    public ObservableCollection<StatusOption> DeviceOptions { get; } = new();
    [ObservableProperty] private string? currentUserName; // 进入页面时赋值实际登录人
    // 投料记录列表（表格2的数据源）
    public ObservableCollection<MaterialInputRecord> MaterialInputRecords { get; } = new();
    // —— 产出记录列表（表2数据源）
    public ObservableCollection<OutputRecord> OutputRecords { get; } = new();
    public event EventHandler? TabChanged;

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
    // 上表选中项（应产出计划行）
    [ObservableProperty] private OutputPlanItem? selectedOutputPlanItem;

    private bool _suppressRemoteUpdate = false;

    public WorkProcessTaskDetailViewModel(IWorkOrderApi api)
    {
        _api = api;
    }
    private StatusOption? _selectedShift;
    public StatusOption? SelectedShift
    {
        get => _selectedShift;
        set
        {
            if (_selectedShift != value)
            {
                _selectedShift = value;
                OnPropertyChanged();

                // 只有在非抑制阶段，才调用后端更新
                if (!_suppressRemoteUpdate)
                    _ = UpdateShiftAsync(value);
            }
        }
    }

    private StatusOption? _selectedDevice;
    public StatusOption? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (_selectedDevice != value)
            {
                _selectedDevice = value;
                OnPropertyChanged();

                if (!_suppressRemoteUpdate)
                    _ = UpdateDeviceAsync(value);
            }
        }
    }
    partial void OnIsBusyChanged(bool value) { NotifyCanExec(); }
    partial void OnIsEditingChanged(bool value) { NotifyCanExec(); }
    partial void OnIsPausedChanged(bool value) { OnPropertyChanged(nameof(PauseResumeText)); NotifyCanExec(); }
    partial void OnActiveTabChanged(DetailTab value)
    {
        IsInputVisible = (value == DetailTab.Input);
        IsOutputVisible = !IsInputVisible;

        OnPropertyChanged(nameof(IsInputTab));
        OnPropertyChanged(nameof(IsOutputTab));
        TabChanged?.Invoke(this, EventArgs.Empty);
    }


    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var v) && v is string id && !string.IsNullOrWhiteSpace(id))
        {
            await InitAsync(id);
        }
    }
    private void NotifyCanExec()
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanPauseResume));
        OnPropertyChanged(nameof(CanFinish));
    }

    [RelayCommand]
    private async Task StartWorkAsync()
    {
        if (IsEditing) return;
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            var resp = await _api.StartWorkAsync(Detail.processCode, Detail.workOrderNo, null);
            if (resp.success && resp.result)
            {
                IsEditing = true;
                IsPaused = false;
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


    [RelayCommand]
    private async Task PauseResumeAsync()
    {
        if (IsBusy || !IsEditing) return;
        IsBusy = true;

        try
        {
            if (!IsPaused)
            {
                // 当前为“运行中”，点击 => 执行“暂停”
                string title = "填写暂停原因";
                string message = "请填写暂停原因（必填）：";
                string accept = "提交";
                string cancel = "取消";

                // 系统弹窗输入（简洁稳妥）
                string? reason = await Application.Current.MainPage.DisplayPromptAsync(
                    title, message, accept, cancel, null, maxLength: 200, keyboard: Keyboard.Text);

                if (reason is null) return;                 // 点击取消
                reason = reason.Trim();
                if (reason.Length == 0)
                {
                    await Application.Current.MainPage.DisplayAlert("提示", "请填写暂停原因。", "知道了");
                    return;
                }

                var resp = await _api.PauseWorkAsync(Detail.processCode, Detail.workOrderNo, reason);
                if (resp.success && resp.result)
                {
                    IsPaused = !IsPaused;
                    await Application.Current.MainPage.DisplayAlert("成功", "已暂停。", "确定");
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("失败", resp.message ?? "暂停失败", "确定");
                }
            }
            else
            {
                // 当前为“已暂停”，点击 => 执行“恢复”
                bool go = await Application.Current.MainPage.DisplayAlert("确认", "确定恢复生产吗？", "恢复", "取消");
                if (!go) return;

                var resp = await _api.PauseWorkAsync(Detail.processCode, Detail.workOrderNo, null);
                if (resp.success && resp.result)
                {
                    IsPaused = false;
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

    [RelayCommand]
    private async Task FinishAsync()
    {
        if (!IsEditing || IsPaused) return;
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            var resp = await _api.CompleteWorkAsync(Detail.processCode, Detail.workOrderNo, null);
            if (resp.success && resp.result)
            {
                IsEditing = false;
                IsPaused = false;
                //await Task.CompletedTask;
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

    [RelayCommand]
    public void ShowInput()
    {
        Debug.WriteLine("切换到投料");
        ActiveTab = DetailTab.Input;
    }

    [RelayCommand]
    public void ShowOutput()
    {
        Debug.WriteLine("切换到产出");
        ActiveTab = DetailTab.Output;  // 同上
    }

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
            CurrentUserName = Preferences.Get("UserName", string.Empty); // 进入页面时赋值实际登录人
        }
        finally { IsBusy = false; }
    }

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
            if (Detail.periodExecute == "working")
            {
                IsEditing = true;
                IsPaused = false;
            }
            else if (Detail.periodExecute == "pause")
            {
                IsEditing = false;
                IsPaused = true;
            }
            else if (Detail.periodExecute == "resume")
            {
                IsEditing = true;
                IsPaused = false;
            }
            else if (Detail.periodExecute == "complete")
            {
                IsEditing = false;
                IsPaused = false;
            }


            // 关键：加载下拉选项
            await LoadShiftsAsync();
            await LoadDevicesAsync();

            // 关键：抑制更新 → 设定选中项
            _suppressRemoteUpdate = true;
            try
            {
                // 班次
                if (!string.IsNullOrWhiteSpace(Detail.teamCode))
                {
                    var shiftOpt = ShiftOptions.FirstOrDefault(x => x.Value == Detail.teamCode)
                                   ?? ShiftOptions.FirstOrDefault(); // 找不到就给“请选择”
                    SelectedShift = shiftOpt;
                }
                else
                {
                    SelectedShift = ShiftOptions.FirstOrDefault(); // “请选择”
                }

                // 设备
                if (!string.IsNullOrWhiteSpace(Detail.productionMachine))
                {
                    var devOpt = DeviceOptions.FirstOrDefault(x => x.Value == Detail.productionMachine)
                                 ?? DeviceOptions.FirstOrDefault();
                    SelectedDevice = devOpt;
                }
                else
                {
                    SelectedDevice = DeviceOptions.FirstOrDefault();
                }
            }
            finally
            {
                _suppressRemoteUpdate = false; // 解除抑制
            }
        }
        else
        {
            // 可视化提示由页面处理
        }
    }

    private async Task LoadShiftsAsync()
    {
        ShiftOptions.Clear();
        if (Detail != null && Detail.factoryCode != null && Detail.factoryCode != null)
        {
            var resp = await _api.GetShiftOptionsAsync(Detail.factoryCode, Detail.workShop);
            // 默认加一个“请选择”
            ShiftOptions.Add(new StatusOption { Text = "请选择", Value = null });
            if (resp != null)
            {
                foreach (var o in resp.result ?? new())
                    ShiftOptions.Add(new StatusOption { Text = o.workshopsName ?? o.workshopsCode, Value = o.workshopsCode });
            }
        }
    }

    private async Task LoadDevicesAsync()
    {
        DeviceOptions.Clear();
        if (Detail != null && Detail.factoryCode != null && Detail.processCode != null)
        {
            var resp = await _api.GetDeviceOptionsAsync(Detail.factoryCode, Detail.processCode);
            DeviceOptions.Add(new StatusOption { Text = "请选择", Value = null });
            if (resp != null)
            {
                foreach (var o in resp.result ?? new())
                    DeviceOptions.Add(new StatusOption { Text = o.deviceName ?? o.deviceCode, Value = o.deviceCode });
            }
        }
    }


    private async Task UpdateShiftAsync(StatusOption? opt)
    {
        if (opt == null || string.IsNullOrWhiteSpace(Detail?.id))
            return;

        try
        {
            IsBusy = true;

            var r = await _api.UpdateWorkProcessTaskAsync(
            id: Detail.id, null, null, null, teamCode: opt.Value, teamName: opt.Text, null, null, null, default);

            if (!r.Succeeded)
                await ShowTip(string.IsNullOrWhiteSpace(r.Message) ? "更新班次失败" : r.Message);
            else
                await ShowTip("班次已更新");
        }
        catch (Exception ex)
        {
            await ShowTip($"更新班次异常：{ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task UpdateDeviceAsync(StatusOption? opt)
    {
        // 基本校验：必须有任务ID和选中项
        if (opt == null || string.IsNullOrWhiteSpace(Detail?.id))
            return;

        try
        {
            IsBusy = true;

            var r = await _api.UpdateWorkProcessTaskAsync(
            id: Detail.id, opt.Value, opt.Text, null, null, null, null, null, null, default);

            if (!r.Succeeded)
                await ShowTip(string.IsNullOrWhiteSpace(r.Message) ? "更新设备失败" : r.Message);
            else
                await ShowTip("设备已更新");
        }
        catch (Exception ex)
        {
            await ShowTip($"更新设备异常：{ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }


    private Task ShowTip(string message) =>
           Shell.Current?.DisplayAlert("提示", message, "确定") ?? Task.CompletedTask;

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

    // 点击“新增投料”
    [RelayCommand]
    private async Task AddMaterialInputAsync()
    {
        // 准备列表（用于“无预设”时给用户选择）
        var list = Detail?.materialInputList ?? Enumerable.Empty<TaskMaterialInput>();

        // 预设物料（有选中就作为预设；否则传 null 让用户自行选择）
        TaskMaterialInput? preset = SelectedMaterialItem is null
            ? null
            : new TaskMaterialInput
            {
                materialCode = SelectedMaterialItem.materialCode,
                materialName = SelectedMaterialItem.materialName
            };

        // 打开弹窗（新重载）：有预设则只输入数量/备注；无预设则先选物料再输入
        var picked = await MaterialInputPopupPage.ShowAsync(_sp, list, preset);
        if (picked is null) return;

        // 统一取“最终物料信息”（优先用预设；没有预设时用弹窗选择结果）
        var finalCode = preset?.materialCode ?? picked.MaterialCode;
        var finalName = preset?.materialName ?? picked.MaterialName;

        // 组装请求
        var req = new AddWorkProcessTaskMaterialInputReq
        {   materialClassName= picked.materialClassName,
            materialCode = finalCode,
            materialName = finalName,
            materialTypeName = picked.materialTypeName,
            qty = (double)picked.Quantity,                    // 从弹窗取
            memo = picked.Memo,
            unit = picked.Unit,
            workOrderNo = Detail.workOrderNo,
            processCode = Detail.processCode,
            processName = Detail.processName,
            schemeNo = Detail.schemeNo,
            platPlanNo = Detail.platPlanNo
        };

        var resp = await _api.AddWorkProcessTaskMaterialInputAsync(req);
        if (!resp.success)
        {
            await Shell.Current.DisplayAlert("失败", resp.message ?? "提交失败", "OK");
            return;
        }

        // 成功：插入下表顶部
        var idx = (MaterialInputRecords.Count == 0) ? 1 : (MaterialInputRecords[0].Index + 1);
        MaterialInputRecords.Insert(0, new MaterialInputRecord
        {
            Index = idx,
            MaterialName = finalName,
            Unit = picked.Unit,
            Qty = (double)picked.Quantity,
            OperationDate = picked.OperationTime,
            Memo = picked.Memo
        });
        SelectedMaterialItem = null;
    }


    // 删除（仅前端）
    [RelayCommand]
    private void DeleteMaterialInput(MaterialInputRecord row)
    {
        if (row == null) return;
        MaterialInputRecords.Remove(row);
        // 如需后端删除，在此调用删除接口

    }

    // 新增产出：只用选中行 + 弹窗返回的数量/备注
    [RelayCommand]
    private async Task AddOutputAsync()
    {
        // 准备列表（用于“无预设”时给用户选择）
        var list = Detail?.materialOutputList ?? Enumerable.Empty<TaskMaterialOutput>();

        // 预设物料（有选中就作为预设；否则传 null 让用户自行选择）
        TaskMaterialOutput? preset = SelectedOutputItem is null
            ? null
            : new TaskMaterialOutput
            {
                materialCode = SelectedOutputItem.materialCode,
                materialName = SelectedOutputItem.materialName
            };

        // 打开弹窗（新重载）：有预设则只输入数量/备注；无预设则先选物料再输入
        var picked = await OutputPopupPage.ShowAsync(_sp, list, preset);
        if (picked is null) return;

        // 统一取“最终物料信息”（优先用预设；没有预设时用弹窗选择结果）
        var finalCode = preset?.materialCode ?? picked.MaterialCode;
        var finalName = preset?.materialName ?? picked.MaterialName;

        // 组装请求
        var req = new AddWorkProcessTaskProductOutputReq
        {
            materialClassName = picked.materialClassName,
            materialCode = finalCode,
            materialName = finalName,
            materialTypeName = picked.materialTypeName,
            qty = (double)picked.Quantity,                    // 从弹窗取
            memo = picked.Memo,
            unit = picked.Unit,
            workOrderNo = Detail.workOrderNo,
            processCode = Detail.processCode,
            processName = Detail.processName,
            schemeNo = Detail.schemeNo,
            platPlanNo = Detail.platPlanNo
        };

        var resp = await _api.AddWorkProcessTaskProductOutputAsync(req);
        if (!resp.success)
        {
            await Shell.Current.DisplayAlert("失败", resp.message ?? "提交失败", "OK");
            return;
        }

        // 成功：插入下表顶部
        var idx = (OutputRecords.Count == 0) ? 1 : (OutputRecords[0].Index + 1);
        OutputRecords.Insert(0, new OutputRecord
        {
            Index = idx,
            MaterialName = finalName,
            Unit = picked.Unit,
            Qty = (double)picked.Quantity,
            OperationDate = picked.OperationTime,
            Memo = picked.Memo
        });
        SelectedOutputItem = null;
        IsInputVisible = false;
        IsOutputVisible = true;
    }

    // 删除（前端移除；如需后端删除在此补接口）
    [RelayCommand]
    private void DeleteOutput(OutputRecord row)
    {
        if (row == null) return;
        OutputRecords.Remove(row);
        //调用删除接口
    }

    [RelayCommand]
    private void MaterialItemSelected(TaskMaterialInput? item)
    {
        if (item != null && !ReferenceEquals(SelectedMaterialItem, item))
            SelectedMaterialItem = item;
    }

    [RelayCommand]
    private void OutputItemSelected(TaskMaterialOutput? item)
    {
        if (item != null && !ReferenceEquals(SelectedOutputItem, item))
            SelectedOutputItem = item;
    }
}
