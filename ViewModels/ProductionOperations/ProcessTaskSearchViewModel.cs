using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JXHLJSApp.Models;
using JXHLJSApp.Pages;
using JXHLJSApp.Services;
using System.Collections.ObjectModel;



namespace JXHLJSApp.ViewModels
{
    public partial class ProcessTaskSearchViewModel : ObservableObject
    {
        private readonly IWorkOrderApi _workapi;

        /// <summary>执行 new 逻辑。</summary>
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private string headerTitle = "生产管理系统";
        [ObservableProperty] private string? keyword;
        [ObservableProperty] private string? machineKeyword;
        [ObservableProperty] private DateTime startDate = DateTime.Today.AddDays(-7);
        [ObservableProperty] private DateTime endDate = DateTime.Today;
        [ObservableProperty] private string? selectedStatus = "全部";
        [ObservableProperty] private int pageIndex = 1;
        [ObservableProperty] private int pageSize = 10;
        [ObservableProperty] private bool isLoadingMore;
        [ObservableProperty] private bool hasMore = true;
        public ObservableCollection<StatusOption> StatusOptions { get; } = new();
        /// <summary>执行 new 逻辑。</summary>
        [ObservableProperty] private StatusOption? selectedStatusOption;

        readonly Dictionary<string, string> _statusMap = new();      // 状态：值→中文
        readonly Dictionary<string, string> _orderstatusMap = new();    // 工序：code→name
        private bool _dictsLoaded = false;

        /// <summary>执行 new 逻辑。</summary>
        public ObservableCollection<ProcessTask> Orders { get; } = new();

        public IAsyncRelayCommand SearchCommand { get; }
        public IRelayCommand ClearCommand { get; }

        /// <summary>执行 ProcessTaskSearchViewModel 初始化逻辑。</summary>
        public ProcessTaskSearchViewModel(IWorkOrderApi workapi)
        {
            _workapi = workapi;
            HeaderTitle = Preferences.Get("WorkShopName", Preferences.Get("WorkshopName", "生产管理系统"));
            if (string.IsNullOrWhiteSpace(HeaderTitle)) HeaderTitle = "生产管理系统";
            SearchCommand = new AsyncRelayCommand(SearchAsync);
            ClearCommand = new RelayCommand(ClearFilters);
            _ = EnsureDictsLoadedAsync();   // fire-and-forget
           
        }
        /// <summary>执行 EnsureDictsLoadedAsync 逻辑。</summary>
        private async Task EnsureDictsLoadedAsync()
        {
            if (_dictsLoaded) return;

            try
            {
                // 1) 状态下拉：来自 getWorkProcessTaskDictList 的 auditStatus
                var bundle = await _workapi.GetWorkProcessTaskDictListAsync(); // ← 你已有的方法名若不同，替换为现有的
                var auditField = bundle?.result?.FirstOrDefault(x =>
                    string.Equals(x.field, "auditStatus", StringComparison.OrdinalIgnoreCase));

                StatusOptions.Clear();
                _statusMap.Clear();
                //StatusOptions.Add(new StatusOption { Text = "全部", Value = null });
                if (auditField?.dictItems != null)
                {
                    foreach (var d in auditField.dictItems)
                    {
                        var val = d.dictItemValue?.Trim();
                        if (string.IsNullOrWhiteSpace(val)) continue;

                        var name = d.dictItemName ?? val;
                        _statusMap[val] = name; // ★ 建立码→名映射
                        if (name == "待执行" || name == "执行中") {
                            StatusOptions.Add(new StatusOption
                            {
                                Text = name,
                                Value = val
                            });
                        } 
                    }
                    
                }
                SelectedStatusOption ??= StatusOptions.FirstOrDefault();

                //3)工单状态
                var orderstatus = await _workapi.GetWorkOrderDictsAsync();
                _orderstatusMap.Clear();
                foreach (var d in orderstatus.AuditStatus)
                    if (!string.IsNullOrWhiteSpace(d.dictItemValue))
                        _orderstatusMap[d.dictItemValue!] = d.dictItemName ?? d.dictItemValue!;
                _dictsLoaded = true;
            }
            catch (Exception ex)
            {
                //if (StatusOptions.Count == 0)
                   // StatusOptions.Add(new StatusOption { Text = "全部", Value = null });
                _dictsLoaded = true;
            }
        }
        /// <summary>执行 SearchAsync 逻辑。</summary>
        public async Task SearchAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                await EnsureDictsLoadedAsync();   // ★ 先确保字典到位

                PageIndex = 1;
                Orders.Clear();
                var records = await LoadPageAsync(PageIndex);
                if (records.Count == 0)
                {
                    await ShowTip("未查询到任何数据");
                }
                else
                {
                    foreach (var t in records)
                        Orders.Add(t);
                }

                HasMore = records.Count >= PageSize;
            }
            finally { IsBusy = false; }
        }

        /// <summary>执行 LoadMoreAsync 逻辑。</summary>
        [RelayCommand]
        private async Task LoadMoreAsync()
        {
            if (IsBusy || IsLoadingMore || !HasMore) return;

            try
            {
                IsLoadingMore = true;
                PageIndex++;
                var records = await LoadPageAsync(PageIndex);
                foreach (var t in records)
                    Orders.Add(t);
                HasMore = records.Count >= PageSize;
            }
            finally
            {
                IsLoadingMore = false;
            }
        }

        /// <summary>执行 LoadPageAsync 逻辑。</summary>
        private async Task<List<ProcessTask>> LoadPageAsync(int pageNo)
        {
            var hasKeyword = !string.IsNullOrWhiteSpace(Keyword);
            var statusList = string.IsNullOrWhiteSpace(SelectedStatusOption?.Value)
            ? null
            : new[] { SelectedStatusOption.Value };

            var page = await _workapi.PageWorkProcessTasksAsync(
                workOrderNo: null,
                auditStatusList: statusList,
                processCode: null,
                createdTimeStart: hasKeyword ? null : StartDate.Date,
                createdTimeEnd: hasKeyword ? null : EndDate.Date.AddDays(1).AddSeconds(-1),
                materialName: hasKeyword ? Keyword?.Trim() : null,
                machine: string.IsNullOrWhiteSpace(MachineKeyword) ? null : MachineKeyword.Trim(),
                pageNo: pageNo,
                pageSize: PageSize,
                ct: CancellationToken.None);

            var records = page?.result?.records ?? new List<ProcessTask>();
            foreach (var t in records)
            {
                if (!string.IsNullOrWhiteSpace(t.AuditStatus) &&
                _statusMap.TryGetValue(t.AuditStatus, out var sName))
                    t.AuditStatusName = sName;
                if (!string.IsNullOrWhiteSpace(t.WorkOrderAuditStatus) &&
               _orderstatusMap.TryGetValue(t.WorkOrderAuditStatus, out var sName2))
                    t.WorkOrderAuditStatus = sName2;
            }

            return records;
        }
        /// <summary>执行 ShowTip 逻辑。</summary>
        private Task ShowTip(string message) =>
           Shell.Current?.DisplayAlert("提示", message, "确定") ?? Task.CompletedTask;

        /// <summary>执行 ClearFilters 逻辑。</summary>
        private void ClearFilters()
        {
            Keyword = string.Empty;
            MachineKeyword = string.Empty;
            SelectedStatus = "全部";
            StartDate = DateTime.Today.AddDays(-7);
            EndDate = DateTime.Today;
            PageIndex = 1;
            HasMore = true;
            SelectedStatusOption = StatusOptions.FirstOrDefault();
            Orders.Clear();
        }


        // 点击一条工单进入执行页
        /// <summary>执行 GoExecuteAsync 逻辑。</summary>
        [RelayCommand]
        private async Task GoExecuteAsync(ProcessTask? item)
        {
            if (item is null) return;
            await Shell.Current.GoToAsync(nameof(WorkProcessTaskDetailPage) + $"?id={Uri.EscapeDataString(item.Id)}");
        }
    }

}
