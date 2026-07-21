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

        if (_devices.All(device => !string.Equals(device.machineNo, _selectedMachineNo, StringComparison.OrdinalIgnoreCase)))
        {
            _selectedMachineNo = _devices.FirstOrDefault()?.machineNo;
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

        if (_devices.Count == 0)
        {
            MachineButtonLayout.Children.Add(CreateMachineButton("全部", null));
            return;
        }

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
            Padding = new Thickness(16, 8),
            CornerRadius = 18,
            MinimumWidthRequest = 86,
            BackgroundColor = isSelected ? Color.FromArgb("#1677FF") : Colors.White,
            BorderColor = isSelected ? Color.FromArgb("#1677FF") : Color.FromArgb("#D8E3F3"),
            BorderWidth = 1,
            TextColor = isSelected ? Colors.White : Color.FromArgb("#0A2E69"),
            FontAttributes = isSelected ? FontAttributes.Bold : FontAttributes.None
        };
        button.Clicked += OnMachineButtonClicked;
        return button;
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
