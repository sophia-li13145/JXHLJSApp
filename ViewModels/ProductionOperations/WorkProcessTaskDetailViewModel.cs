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
    [ObservableProperty] private string? reportQty; // 绑定报工数量输入框
    // 投料记录列表（表格2的数据源）
    public ObservableCollection<MaterialInputRecord> MaterialInputRecords { get; } = new();
    // —— 产出记录列表（表2数据源）
    public ObservableCollection<OutputRecord> OutputRecords { get; } = new();
    public event EventHandler? TabChanged;

    // ① 新增：被选中的上表行
    [ObservableProperty] private MaterialInputItem? selectedMaterialItem;
    // 上表选中项（应产出计划行）
    [ObservableProperty] private OutputPlanItem? selectedOutputPlanItem;

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

                // 选中就调用后端更新
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

                // 选中就调用后端更新
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
        if (IsBusy  || !IsEditing) return;
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
            await LoadShiftsAsync();
            await LoadDevicesAsync();

            // 初始：不可编辑，且默认展示投料
            IsEditing = false;
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
            Detail = resp.result;

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


            // 选中下拉（如果后端返回了默认值）
            if (!string.IsNullOrWhiteSpace(Detail.teamCode))
                _selectedShift = ShiftOptions.FirstOrDefault(o => o.Value == Detail.teamCode);
            if (!string.IsNullOrWhiteSpace(Detail.productionMachine))
                SelectedDevice = DeviceOptions.FirstOrDefault(o => o.Value == Detail.productionMachine);
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
            id: Detail.id, opt.Value, opt.Text, null, null, null, null, null, null,default);

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
        if (string.IsNullOrWhiteSpace(ReportQty))
        {
            await Shell.Current.DisplayAlert("提示", "请输入数量", "OK");
            return;
        }

        var qty = int.TryParse(ReportQty, out var value) ? value : 0;
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
        if (SelectedMaterialItem is null)
        {
            await Shell.Current.DisplayAlert("提示", "请先在上方表格选择一条物料。", "OK");
            return;
        }

        // ② 弹窗：只输入数量和备注；物料名只读显示
        var picked = await MaterialInputPopupPage.ShowAsync(_sp, SelectedMaterialItem);
        if (picked is null) return;

        // ③ 组装请求并调用接口
        var req = new AddWorkProcessTaskMaterialInputReq
        {
            materialClassName = SelectedMaterialItem.materialClassName,
            materialCode = SelectedMaterialItem.materialCode,
            materialName = SelectedMaterialItem.materialName,
            materialTypeName = SelectedMaterialItem.materialTypeName,
            unit = SelectedMaterialItem.unit,
            qty = picked.Qty, // 只从弹窗取数量
            memo = picked.Memo,
            rawMaterialProductionDate = picked.RawMaterialProductionDate?.ToString("yyyy-MM-dd HH:mm:ss"),
            workOrderNo = Detail.workOrderNo,
            processCode = Detail.processCode,
            processName = Detail.processName,
            //schemeNo = string.IsNullOrWhiteSpace(Detail.schemeNo) ? null : SchemeNo,
            //platPlanNo = string.IsNullOrWhiteSpace(Detail.pl) ? null : PlatPlanNo
        };

        var resp = await _api.AddWorkProcessTaskMaterialInputAsync(req);
        if (!resp.success)
        {
            await Shell.Current.DisplayAlert("失败", resp.message ?? "提交失败", "OK");
            return;
        }

        // ④ 成功：插入下表顶部
        var idx = (MaterialInputRecords.Count == 0) ? 1 : (MaterialInputRecords[0].Index + 1);
        MaterialInputRecords.Insert(0, new MaterialInputRecord
        {
            Index = idx,
            MaterialName = SelectedMaterialItem.materialName,
            Unit = SelectedMaterialItem.unit,
            Qty = picked.Qty,
            RawMaterialProductionDate = picked.RawMaterialProductionDate,
            Memo = picked.Memo
        });
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
        if (SelectedOutputPlanItem is null)
        {
            await Shell.Current.DisplayAlert("提示", "请先在上方表格选择一条物料。", "OK");
            return;
        }

        // 弹窗：物料只读，输入数量与备注
        var picked = await OutputPopupPage.ShowAsync(_sp, SelectedOutputPlanItem);
        if (picked is null) return;

        var now = DateTime.Now; // 操作时间：客户端取当前时间（或后端生成）

        var req = new AddWorkProcessTaskProductOutputReq
        {
            materialClassName = SelectedOutputPlanItem.materialClassName,
            materialCode = SelectedOutputPlanItem.materialCode,
            materialName = SelectedOutputPlanItem.materialName,
            materialTypeName = SelectedOutputPlanItem.materialTypeName,
            unit = SelectedOutputPlanItem.unit,
            qty = picked.Qty,
            memo = picked.Memo,
            operateTime = now.ToString("yyyy-MM-dd HH:mm:ss"),

            workOrderNo = Detail.workOrderNo,
            processCode = Detail.processCode,
            processName = Detail.processName,
            //schemeNo = string.IsNullOrWhiteSpace(Detail.schemeNo) ? null : SchemeNo,
            //platPlanNo = string.IsNullOrWhiteSpace(Detail.pl) ? null : PlatPlanNo
        };

        var resp = await _api.AddWorkProcessTaskProductOutputAsync(req);
        if (!resp.success)
        {
            await Shell.Current.DisplayAlert("失败", resp.message ?? "提交失败", "OK");
            return;
        }

        // UI：插入到产出记录表顶部
        var idx = (OutputRecords.Count == 0) ? 1 : (OutputRecords[0].Index + 1);
        OutputRecords.Insert(0, new OutputRecord
        {
            Index = idx,
            MaterialName = SelectedOutputPlanItem.materialName,
            Unit = SelectedOutputPlanItem.unit,
            Qty = picked.Qty,
            OperateTime = now,
            Memo = picked.Memo
        });
    }

    // 删除（前端移除；如需后端删除在此补接口）
    [RelayCommand]
    private void DeleteOutput(OutputRecord row)
    {
        if (row == null) return;
        OutputRecords.Remove(row);
    }
}
