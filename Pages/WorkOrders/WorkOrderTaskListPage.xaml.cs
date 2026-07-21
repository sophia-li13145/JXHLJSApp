using JXHLJSApp.Services;
using JXHLJSApp.Models.WorkOrders;
using JXHLJSApp.Services.WorkOrders;

namespace JXHLJSApp.Pages.WorkOrders;

public partial class WorkOrderTaskListPage : ContentPage
{
    private readonly IWorkOrderApi _workOrderApi;
    private List<DeviceDto> _devices = new();
    private string? _selectedMachineNo;

    public WorkOrderTaskListPage(IWorkOrderApi workOrderApi)
    {
        InitializeComponent();
        _workOrderApi = workOrderApi;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadPageAsync();
    }

    private async void OnRefreshing(object sender, EventArgs e) => await LoadPageAsync();

    private async Task LoadPageAsync()
    {
        try
        {
            RefreshContainer.IsRefreshing = true;
            await LoadDevicesAsync();
            await LoadTasksAsync();
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "加载失败", ex.Message, "确定");
        }
        finally
        {
            RefreshContainer.IsRefreshing = false;
        }
    }

    private async Task LoadDevicesAsync()
    {
        var devices = await _workOrderApi.GetDeviceListAsync();
        _devices = devices
            .Where(device => !string.IsNullOrWhiteSpace(device.machineNo))
            .GroupBy(device => device.machineNo!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (!string.IsNullOrWhiteSpace(_selectedMachineNo)
            && _devices.All(device => !string.Equals(device.machineNo, _selectedMachineNo, StringComparison.OrdinalIgnoreCase)))
        {
            _selectedMachineNo = null;
        }

        BuildMachineButtons();
    }

    private async Task LoadTasksAsync()
    {
        TaskList.ItemsSource = string.IsNullOrWhiteSpace(_selectedMachineNo)
            ? await _workOrderApi.GetWorkOrderListAsync()
            : await _workOrderApi.GetWorkOrderListAsync(machineNo: _selectedMachineNo);
    }

    private void BuildMachineButtons()
    {
        MachineButtonLayout.Children.Clear();

        MachineButtonLayout.Children.Add(CreateMachineButton("全部机台", null));

        foreach (var device in _devices)
        {
            MachineButtonLayout.Children.Add(CreateMachineButton(device.displayName, device.machineNo));
        }
    }

    private Button CreateMachineButton(string text, string? machineNo)
    {
        var isSelected = string.Equals(machineNo, _selectedMachineNo, StringComparison.OrdinalIgnoreCase);
        var button = new Button
        {
            Text = text,
            CommandParameter = machineNo,
            Padding = new Thickness(20, 10),
            CornerRadius = 20,
            MinimumWidthRequest = 100,
            BackgroundColor = isSelected ? Color.FromArgb("#1F447E") : Colors.White,
            BorderColor = isSelected ? Color.FromArgb("#1F447E") : Color.FromArgb("#D8E3F3"),
            BorderWidth = 1,
            TextColor = isSelected ? Colors.White : Color.FromArgb("#0A2E69"),
            FontAttributes = isSelected ? FontAttributes.Bold : FontAttributes.None
        };
        button.Clicked += OnMachineButtonClicked;
        return button;
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnInstructionClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string id } || string.IsNullOrWhiteSpace(id))
        {
            await DisplayAlert("提示", "工单列表主键为空，无法查看生产作业指令卡。", "确定");
            return;
        }

        await Shell.Current.GoToAsync($"{AppShell.RouteWorkOrderInstruction}?id={Uri.EscapeDataString(id)}");
    }

    private async void OnMachineButtonClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        var machineNo = button.CommandParameter as string;
        if (string.Equals(machineNo, _selectedMachineNo, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _selectedMachineNo = machineNo;
        BuildMachineButtons();

        try
        {
            RefreshContainer.IsRefreshing = true;
            await LoadTasksAsync();
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "加载失败", ex.Message, "确定");
        }
        finally
        {
            RefreshContainer.IsRefreshing = false;
        }
    }
}
