using JXHLJSApp.Models.Quality;

namespace JXHLJSApp.Pages.Quality;

[QueryProperty(nameof(ResourceCode), "resourceCode")]
public partial class MachineQualityTaskListPage : ContentPage
{
    private string? _resourceCode;

    public string? ResourceCode
    {
        get => _resourceCode;
        set => _resourceCode = Uri.UnescapeDataString(value ?? string.Empty);
    }

    public MachineQualityTaskListPage() => InitializeComponent();

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var tasks = MachineQualityTaskStore.GetTasks(_resourceCode).ToList();
        TaskList.ItemsSource = tasks;
        TitleLabel.Text = $"{tasks.FirstOrDefault()?.machineDisplay ?? _resourceCode ?? "机台"} 任务";
    }

    private async void OnRescanTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnTaskTapped(object sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not ProductionQualityOrderDto item) return;
        if (string.IsNullOrWhiteSpace(item.qualityNo) || string.IsNullOrWhiteSpace(item.orderNumber))
        {
            await DisplayAlert("提示", "质检单号或工单号为空，无法查询详情。", "确定");
            return;
        }

        await Shell.Current.GoToAsync($"{AppShell.RouteMachineQualityDetail}?qualityNo={Uri.EscapeDataString(item.qualityNo)}&workOrderNo={Uri.EscapeDataString(item.orderNumber)}&inspectStatus={Uri.EscapeDataString(item.inspectStatus ?? string.Empty)}&workOrderStatus={Uri.EscapeDataString(item.workOrderStatus ?? string.Empty)}");
    }
}
