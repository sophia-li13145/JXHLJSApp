using IndustrialControlMAUI.Models;
using IndustrialControlMAUI.Services;
using IndustrialControlMAUI.ViewModels;
using System.Text.Json;

namespace IndustrialControlMAUI.Pages;

public partial class WorkOrderSearchPage : ContentPage
{
    private readonly ScanService _scanSvc;
    private readonly WorkOrderSearchViewModel _vm;

    public WorkOrderSearchPage(WorkOrderSearchViewModel vm, ScanService scanSvc)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;

    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        OrderEntry.Focus();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
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
        OrderEntry.Text = result.Trim();

        // 同步到 ViewModel
        if (BindingContext is WorkOrderSearchViewModel vm)
        {
            vm.Keyword = result.Trim();

            // 可选：扫码后自动触发查询
            if (vm.SearchCommand.CanExecute(null))
                vm.SearchCommand.Execute(null);
        }
    }


    // 点整张卡片跳转（推荐）

    private async void OnOrderTapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (e.Parameter is not WorkOrderDto item) return;

            var json = JsonSerializer.Serialize(item);

            await Shell.Current.GoToAsync(
                nameof(MoldOutboundExecutePage),
                new Dictionary<string, object?>
                {
                    // 执行页用 IQueryAttributable 接收：key 必须叫 "orderDto"
                    ["orderDto"] = json
                });
        }
        catch (Exception ex)
        {
            await DisplayAlert("导航失败", ex.Message, "确定");
        }
    }


    private async void OnScanHintClicked(object sender, EventArgs e)
            => await DisplayAlert("提示", "此按钮预留摄像头扫码；硬件扫描直接扣扳机。", "确定");
}
