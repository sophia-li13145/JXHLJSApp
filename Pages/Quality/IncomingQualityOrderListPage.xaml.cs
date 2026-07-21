using JXHLJSApp.Services;
using JXHLJSApp.Models.Quality;
using JXHLJSApp.Services.Quality;
using Microsoft.Maui.Controls.Shapes;

namespace JXHLJSApp.Pages.Quality;

public partial class IncomingQualityOrderListPage : ContentPage
{
    private readonly IQualityApi _qualityApi;
    private List<IncomingQualityStatusFilter> _filters = new();
    private string? _selectedStatus;

    public IncomingQualityOrderListPage(IQualityApi qualityApi)
    {
        InitializeComponent();
        _qualityApi = qualityApi;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadFiltersAndOrdersAsync();
    }

    private async void OnRefreshing(object sender, EventArgs e) => await LoadOrdersAsync();

    private async Task LoadFiltersAndOrdersAsync()
    {
        try
        {
            RefreshContainer.IsRefreshing = true;
            _filters = await _qualityApi.GetIncomingQualityStatusFiltersAsync();
            _selectedStatus = _filters.FirstOrDefault()?.Value;
            RenderStatusFilters();
            await LoadOrdersAsync(false);
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

    private async Task LoadOrdersAsync(bool showRefreshing = true)
    {
        try
        {
            if (showRefreshing) RefreshContainer.IsRefreshing = true;
            OrderList.ItemsSource = await _qualityApi.GetIncomingQualityOrdersAsync(_selectedStatus);
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "加载失败", ex.Message, "确定");
        }
        finally
        {
            if (showRefreshing) RefreshContainer.IsRefreshing = false;
        }
    }

    private void RenderStatusFilters()
    {
        StatusFilterStack.Children.Clear();
        foreach (var filter in _filters)
        {
            filter.IsSelected = string.Equals(filter.Value, _selectedStatus, StringComparison.Ordinal);
            var button = new Border
            {
                BackgroundColor = filter.IsSelected ? Color.FromArgb("#173B78") : Colors.White,
                Stroke = filter.IsSelected ? Color.FromArgb("#173B78") : Color.FromArgb("#DEE5EE"),
                StrokeThickness = 1,
                Padding = new Thickness(22, 10),
                MinimumWidthRequest = 74,
                StrokeShape = new RoundRectangle { CornerRadius = 22 },
                Shadow = filter.IsSelected ? new Shadow { Brush = Color.FromArgb("#33000000"), Offset = new Point(0, 4), Radius = 8 } : null,
                Content = new Label
                {
                    Text = filter.Name,
                    TextColor = filter.IsSelected ? Colors.White : Color.FromArgb("#4D5D73"),
                    FontSize = 14,
                    FontAttributes = filter.IsSelected ? FontAttributes.Bold : FontAttributes.None,
                    HorizontalTextAlignment = TextAlignment.Center
                }
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                _selectedStatus = filter.Value;
                RenderStatusFilters();
                await LoadOrdersAsync();
            };
            button.GestureRecognizers.Add(tap);
            StatusFilterStack.Children.Add(button);
        }
    }

    private async void OnOrderTapped(object sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not IncomingQualityOrderDto item) return;
        if (string.IsNullOrWhiteSpace(item.incomingQualityNo))
        {
            await DisplayAlert("提示", "来料质检单号为空，无法查看详情。", "确定");
            return;
        }

        await Shell.Current.GoToAsync($"{AppShell.RouteIncomingQualityOrderDetail}?incomingQualityNo={Uri.EscapeDataString(item.incomingQualityNo)}");
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not IncomingQualityOrderDto item) return;
        await DeleteOrderAsync(item.incomingQualityNo, refreshAfterDelete: true);
    }

    private async Task DeleteOrderAsync(string? incomingQualityNo, bool refreshAfterDelete)
    {
        if (string.IsNullOrWhiteSpace(incomingQualityNo))
        {
            await DisplayAlert("提示", "来料质检单号为空，无法删除。", "确定");
            return;
        }

        var confirm = await DisplayAlert("确认删除", $"确定删除来料质检单 {incomingQualityNo} 吗？", "删除", "取消");
        if (!confirm) return;

        try
        {
            var deleted = await _qualityApi.DeleteIncomingQualityOrderAsync(incomingQualityNo);
            if (!deleted)
            {
                await ErrorDialogService.ShowAsync(this, "删除失败", "接口未返回删除成功，请稍后重试。", "确定");
                return;
            }

            await DisplayAlert("删除成功", "来料质检单已删除。", "确定");
            if (refreshAfterDelete)
            {
                await LoadOrdersAsync();
            }
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "删除失败", ex.Message, "确定");
        }
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
