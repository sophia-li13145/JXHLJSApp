using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndustrialControlMAUI.Models;
using IndustrialControlMAUI.Services;
using IntelliJ.Lang.Annotations;
using System.Collections.ObjectModel;

namespace IndustrialControlMAUI.ViewModels
{
    /// <summary>
    /// 库存查询 VM：通过条码查询库存信息
    /// </summary>
    public partial class InventorySearchViewModel : ObservableObject
    {
        private readonly IWorkOrderApi _api;
        private readonly SemaphoreSlim _scanLock = new(1, 1);

        public InventorySearchViewModel(IWorkOrderApi api)
        {
            _api = api;
        }

        /// <summary>
        /// 扫码/输入的条码（库位码或物料条码）
        /// </summary>
        [ObservableProperty]
        private string? scanCode;

        /// <summary>
        /// 是否正在查询，用于按钮禁用/Loading
        /// </summary>
        [ObservableProperty]
        private bool isBusy;

        /// <summary>
        /// 查询结果库存列表
        /// </summary>
        public ObservableCollection<InventoryRecord> InventoryList { get; } = new();

        // ================== 命令：扫码提交 ==================

        /// <summary>
        /// 扫码完成后调用 / 查询按钮复用
        /// </summary>
        [RelayCommand]
        private async Task ScanSubmit()
        {
            var code = ScanCode?.Trim();
            if (string.IsNullOrEmpty(code))
            {
                await ShowTip("请输入或扫描条码。");
                return;
            }

            await QueryInventoryAsync(code);
            ScanCode = string.Empty;
        }

        /// <summary>
        /// 查询按钮可以直接绑定这个命令（和 ScanSubmit 共用逻辑）
        /// </summary>
        [RelayCommand]
        private Task Search() => ScanSubmit();

        // ================== 核心查询逻辑 ==================

        public async Task QueryInventoryAsync(string barcode)
        {
            await _scanLock.WaitAsync();
            try
            {
                IsBusy = true;

                // 调用接口：只查第一页 50 条
                var resp = await _api.PageInventoryAsync(
                    barcode: barcode,
                    pageNo: 1,
                    pageSize: 50,
                    searchCount: false);

                InventoryList.Clear();

                if (resp == null || resp.success != true || resp.result == null)
                {
                    var msg = string.IsNullOrWhiteSpace(resp?.message)
                        ? "查询库存失败，请稍后重试。"
                        : resp!.message!;
                    await ShowTip(msg);
                    return;
                }
                var i = 1;
                var records = resp.result.records ?? new List<InventoryRecord>();
                foreach (var r in records)
                {
                    r.index = i++;
                    InventoryList.Add(r);
                } 

                if (InventoryList.Count == 0)
                {
                    await ShowTip("未查询到对应的库存信息。");
                }
            }
            catch (Exception ex)
            {
                await ShowTip("查询异常：" + ex.Message);
            }
            finally
            {
                IsBusy = false;
                _scanLock.Release();
            }
        }

        // ================== 辅助方法 ==================

        private Task ShowTip(string msg) =>
            Shell.Current?.DisplayAlert("提示", msg, "确定") ?? Task.CompletedTask;
    }
}
