using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndustrialControlMAUI.Services;

namespace IndustrialControlMAUI.ViewModels
{
    /// <summary>
    /// 出库-模具页面 ViewModel
    /// 依赖 IMoldApi.GetViewAsync(workOrderNo) 获取「型号+基础需求数量+模具编码列表」视图
    /// </summary>
    public partial class OutboundMoldViewModel : ObservableObject, IQueryAttributable
    {
        private readonly IMoldApi _api;

        #region 构造 & 注入
        public OutboundMoldViewModel(IMoldApi api)
        {
            _api = api;

            MoldGroups = new ObservableCollection<MoldGroupVM>();
            ScannedList = new ObservableCollection<ScannedRow>();

            ShowScannedCommand = new RelayCommand(() =>
            {
                IsScannedVisible = true;
                ScannedTabColor = "#2196F3";
                ScannedTextColor = "White";
            });

            CancelScanCommand = new AsyncRelayCommand(CancelScanAsync);
            ConfirmCommand = new AsyncRelayCommand(ConfirmAsync);
            ScanSubmitCommand = new RelayCommand(ScanSubmit);
        }

        // 无 DI 场景可用的辅助构造（可按需删除）
        public OutboundMoldViewModel() : this(
            (IMoldApi)App.Current!.Handler!.MauiContext!.Services.GetService(typeof(IMoldApi))!)
        { }
        #endregion

        #region 顶部信息 & 输入
        [ObservableProperty] private string? workOrderNo;
        [ObservableProperty] private string? materialName;
        [ObservableProperty] private string? scanCode;
        #endregion

        #region 分组（一级=型号/数量，二级=模具编码列表）
        public ObservableCollection<MoldGroupVM> MoldGroups { get; }
        #endregion

        #region 扫描明细
        public ObservableCollection<ScannedRow> ScannedList { get; }

        [ObservableProperty] private ScannedRow? selectedScanItem;

        [ObservableProperty] private bool isScannedVisible = true;
        [ObservableProperty] private string scannedTabColor = "#2196F3";
        [ObservableProperty] private string scannedTextColor = "White";
        #endregion

        #region 命令
        public ICommand ShowScannedCommand { get; }
        public IAsyncRelayCommand CancelScanCommand { get; }
        public IAsyncRelayCommand ConfirmCommand { get; }
        public ICommand ScanSubmitCommand { get; }
        #endregion

        #region 加载
        public async Task LoadAsync(string workOrderNo)
        {
            WorkOrderNo = workOrderNo;

            var view = await _api.GetViewAsync(workOrderNo);
            MaterialName = view.MaterialName;

            MoldGroups.Clear();
            foreach (var m in view.Models)
            {
                var group = new MoldGroupVM(m.ModelCode, m.BaseQty);
                int idx = 1;
                foreach (var code in m.MoldNumbers)
                {
                    group.Items.Add(new MoldItemVM { Index = idx++, MoldNumber = code });
                }
                MoldGroups.Add(group);
            }

            // 默认展开第一组（可按需注释）
            if (MoldGroups.Count > 0)
                MoldGroups[0].SetExpanded(true);
        }
        #endregion

        #region 扫码加入明细（支持扫“模具编码”，或扫“型号”批量加入）
        private void ScanSubmit()
        {
            var code = (ScanCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(code))
                return;

            // 1) 先按“模具编码”匹配二级
            var hit = MoldGroups
                .SelectMany(g => g.Items.Select(i => new { g, i }))
                .FirstOrDefault(x => string.Equals(x.i.MoldNumber, code, System.StringComparison.OrdinalIgnoreCase));

            if (hit != null)
            {
                AddOrIncreaseScanned(hit.i.MoldNumber!, hit.g.ModelCode);
                ScanCode = string.Empty;
                return;
            }

            // 2) 再按“型号”匹配一级（批量加入该型号下所有模具编码）
            var group = MoldGroups.FirstOrDefault(g =>
                string.Equals(g.ModelCode, code, System.StringComparison.OrdinalIgnoreCase));

            if (group != null)
            {
                foreach (var item in group.Items)
                    AddOrIncreaseScanned(item.MoldNumber!, group.ModelCode);

                ScanCode = string.Empty;
                return;
            }

            // 3) 未匹配到
            Application.Current?.MainPage?.DisplayAlert("提示", $"未在当前工单模型中找到：{code}", "知道了");
        }

        private void AddOrIncreaseScanned(string moldCode, string modelCode)
        {
            // 若已存在同编码，则数量 +1；否则新增一行
            var exist = ScannedList.FirstOrDefault(x =>
                string.Equals(x.MoldCode, moldCode, System.StringComparison.OrdinalIgnoreCase));

            if (exist != null)
            {
                exist.OutQty += 1;
                return;
            }

            ScannedList.Add(new ScannedRow
            {
                Index = ScannedList.Count + 1,
                MoldCode = moldCode,
                MoldModel = modelCode,
                OutQty = 1,
                Location = string.Empty,
                IsSelected = false
            });
        }
        #endregion

        #region 取消扫描（支持勾选多选移除）
        private async Task CancelScanAsync()
        {
            var toRemove = ScannedList.Where(x => x.IsSelected).ToList();
            if (toRemove.Count == 0)
            {
                await Application.Current.MainPage.DisplayAlert("提示", "请勾选要取消的记录。", "知道了");
                return;
            }

            foreach (var r in toRemove)
                ScannedList.Remove(r);

            // 重新编号
            for (int i = 0; i < ScannedList.Count; i++)
                ScannedList[i].Index = i + 1;

            await Application.Current.MainPage.DisplayAlert("提示", "已取消选择的扫描记录。", "OK");
        }
        #endregion

        #region 确认出库
        private async Task ConfirmAsync()
        {
            if (string.IsNullOrWhiteSpace(WorkOrderNo))
            {
                await Application.Current.MainPage.DisplayAlert("错误", "缺少工单号，无法确认出库。", "OK");
                return;
            }
            if (ScannedList.Count == 0)
            {
                bool goOn = await Application.Current.MainPage.DisplayAlert("提示", "当前扫描明细为空，确定继续出库？", "继续", "取消");
                if (!goOn) return;
            }

            // TODO: 这里根据你项目中「出库确认」的请求模型补齐构造（字段名可能为示例）：
            // var req = new MoldOutConfirmReq
            // {
            //     workOrderNo = WorkOrderNo,
            //     // 例如：details / outStockDetailList / items
            //     details = ScannedList.Select(x => new MoldOutDetail
            //     {
            //         moldCode = x.MoldCode,
            //         model = x.MoldModel,
            //         outQty = x.OutQty,
            //         location = x.Location
            //     }).ToList()
            // };
            //
            // var ok = await _api.ConfirmOutStockAsync(req);
            // if (!ok.Succeeded)
            // {
            //     await Application.Current.MainPage.DisplayAlert("错误", ok.Message ?? "确认出库失败。", "OK");
            //     return;
            // }

            // 为保证当前文件可编译运行，先给出占位成功提示：
            await Application.Current.MainPage.DisplayAlert("成功（占位）", "已调用出库确认，请接入请求模型后提交后端。", "OK");
            ScannedList.Clear();
        }
        #endregion
        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("workOrderNo", out var v) && v is string no && !string.IsNullOrWhiteSpace(no))
            {
                // 异步加载放到主线程调度
                MainThread.BeginInvokeOnMainThread(async () => await LoadAsync(no));
            }
        }
    }

    #region 分组子VM
    public partial class MoldGroupVM : ObservableObject
    {
        public MoldGroupVM(string modelCode, int baseQty)
        {
            ModelCode = modelCode;
            BaseQty = baseQty;

            Items = new ObservableCollection<MoldItemVM>();
            ToggleCommand = new RelayCommand(() =>
            {
                IsExpanded = !IsExpanded;
                ToggleIndicator = IsExpanded ? "-" : "+";
            });
        }

        [ObservableProperty] private string modelCode;
        [ObservableProperty] private int baseQty;

        [ObservableProperty] private bool isExpanded = false;
        [ObservableProperty] private string toggleIndicator = "+";

        public ObservableCollection<MoldItemVM> Items { get; }

        public ICommand ToggleCommand { get; }

        public void SetExpanded(bool expanded)
        {
            IsExpanded = expanded;
            ToggleIndicator = expanded ? "-" : "+";
        }
    }

    public partial class MoldItemVM : ObservableObject
    {
        [ObservableProperty] private int index;
        [ObservableProperty] private string? moldNumber;
    }

    public partial class ScannedRow : ObservableObject
    {
        [ObservableProperty] private bool isSelected;
        [ObservableProperty] private int index;
        [ObservableProperty] private string? moldCode;
        [ObservableProperty] private string? moldModel;
        [ObservableProperty] private int outQty;
        [ObservableProperty] private string? location;
    }
    #endregion
}
