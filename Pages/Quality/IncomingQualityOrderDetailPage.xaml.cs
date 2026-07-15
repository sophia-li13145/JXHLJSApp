using JXHLJSApp.Models.Quality;
using JXHLJSApp.Services;
using JXHLJSApp.Services.Quality;
using Microsoft.Maui.Controls.Shapes;
using System.Collections.ObjectModel;

namespace JXHLJSApp.Pages.Quality;

[QueryProperty(nameof(IncomingQualityNo), "incomingQualityNo")]
public partial class IncomingQualityOrderDetailPage : ContentPage
{
    private readonly IQualityApi _qualityApi;
    private readonly ObservableCollection<IncomingQualityScanDetailDto> _records = new();
    private string? _incomingQualityNo;
    private IncomingQualityOrderDetailDto? _detail;

    public string? IncomingQualityNo
    {
        get => _incomingQualityNo;
        set => _incomingQualityNo = Uri.UnescapeDataString(value ?? string.Empty);
    }

    public IncomingQualityOrderDetailPage(IQualityApi qualityApi, IScanService scanService)
    {
        InitializeComponent();
        _qualityApi = qualityApi;
        ScanRecordList.ItemsSource = _records;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDetailAsync();
    }

    private async void OnRefreshing(object sender, EventArgs e) => await LoadDetailAsync();

    private async Task LoadDetailAsync()
    {
        if (string.IsNullOrWhiteSpace(_incomingQualityNo))
        {
            await DisplayAlert("提示", "来料质检单号为空，无法查询详情。", "确定");
            return;
        }

        try
        {
            RefreshContainer.IsRefreshing = true;
            _detail = await _qualityApi.GetIncomingQualityOrderDetailAsync(_incomingQualityNo);
            QualityNoLabel.Text = _detail.incomingQualityNoDisplay;
            InstockNoLabel.Text = _detail.instockNoDisplay;
            StatusLabel.Text = _detail.statusDisplay;
            ScanCountLabel.Text = $"共 {_detail.scanCount} 次";
            MaterialNameLabel.Text = _detail.materialNameDisplay;
            SpecLabel.Text = _detail.specDisplay;
            TotalLabel.Text = _detail.totalDisplay;
            var showMaterial = !_detail.isUnsubmitted;
            MaterialNameTitle.IsVisible = showMaterial;
            MaterialNameLabel.IsVisible = showMaterial;
            SpecTitle.IsVisible = showMaterial;
            SpecLabel.IsVisible = showMaterial;
            TotalTitle.IsVisible = showMaterial;
            TotalLabel.IsVisible = showMaterial;
            ApplyStatusStyle(_detail);
            ScanRecordsHeader.IsVisible = true;
            ScanRecordList.IsVisible = true;
            BuildActionBar(_detail);

            _records.Clear();
            foreach (var record in _detail.scanDetails)
            {
                _records.Add(record);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("加载失败", ex.Message, "确定");
        }
        finally
        {
            RefreshContainer.IsRefreshing = false;
        }
    }

    private void ApplyStatusStyle(IncomingQualityOrderDetailDto detail)
    {
        var (background, text) = detail.statusDisplay switch
        {
            "待质检" => ("#FFF6E8", "#D97706"),
            "已完成" or "检验完成" => ("#E9FBEF", "#16A34A"),
            _ => ("#F2F4F8", "#051B3D")
        };
        StatusBadge.BackgroundColor = Color.FromArgb(background);
        StatusLabel.TextColor = Color.FromArgb(text);
    }

    private void BuildActionBar(IncomingQualityOrderDetailDto detail)
    {
        ActionBar.Children.Clear();
        ActionBar.ColumnDefinitions.Clear();
        if (detail.isCompleted)
        {
            ActionBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            ActionBar.Add(CreateOutlineAction("返回列表", OnBackButtonClicked), 0);
            return;
        }

        ActionBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        ActionBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        if (detail.isUnsubmitted)
        {
            ActionBar.Add(CreateButton("质检扫码", "White", "#F59E0B", null, OnScanClicked), 0);
            ActionBar.Add(CreateButton("完成检验", "White", "#10B981", null, OnCompleteClicked), 1);
            return;
        }

        if (detail.isWaitInspection)
        {
            ActionBar.Add(CreateButton("质检扫码", "White", "#F59E0B", null, OnScanClicked), 0);
            ActionBar.Add(CreateButton("完成检验", "White", "#10B981", null, OnCompleteClicked), 1);
            return;
        }

        ActionBar.Add(CreateButton("删除", "#FF4D5E", "#FFF2F2", "#FFB7BE", OnDeleteClicked), 0);
        ActionBar.Add(CreateButton("质检扫码", "White", "#1E427C", null, OnScanClicked), 1);
    }

    private static Border CreateOutlineAction(string text, EventHandler tapped)
    {
        var border = new Border
        {
            BackgroundColor = Colors.White,
            Stroke = Color.FromArgb("#C8D2DF"),
            StrokeThickness = 1,
            HeightRequest = 56,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Content = new Label
            {
                Text = text,
                TextColor = Color.FromArgb("#051B3D"),
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => tapped(border, EventArgs.Empty);
        border.GestureRecognizers.Add(tap);
        return border;
    }

    private static Button CreateButton(string text, string textColor, string backgroundColor, string? borderColor, EventHandler clicked)
    {
        var button = new Button
        {
            Text = text,
            TextColor = Color.FromArgb(textColor),
            BackgroundColor = Color.FromArgb(backgroundColor),
            CornerRadius = 10,
            HeightRequest = 56,
            FontAttributes = FontAttributes.Bold
        };
        if (!string.IsNullOrWhiteSpace(borderColor))
        {
            button.BorderColor = Color.FromArgb(borderColor);
            button.BorderWidth = 1;
        }
        button.Clicked += clicked;
        return button;
    }

    private async void OnScanClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"{AppShell.RouteIncomingQualityScan}?incomingQualityNo={Uri.EscapeDataString(_incomingQualityNo ?? string.Empty)}");
    }

    private async void OnCompleteClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_incomingQualityNo)) return;
        var confirm = await DisplayAlert("完成检验", $"确定完成来料质检单 {_incomingQualityNo} 吗？", "完成", "取消");
        if (!confirm) return;

        try
        {
            var completed = await _qualityApi.CompleteIncomingQualityOrderAsync(_incomingQualityNo);
            if (!completed)
            {
                await DisplayAlert("完成失败", "接口未返回完成成功，请稍后重试。", "确定");
                return;
            }
            await DisplayAlert("完成成功", "来料质检单已完成。", "确定");
            await LoadDetailAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("完成失败", ex.Message, "确定");
        }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_incomingQualityNo))
        {
            await DisplayAlert("提示", "来料质检单号为空，无法删除。", "确定");
            return;
        }

        var confirm = await DisplayAlert("确认删除", $"确定删除来料质检单 {_incomingQualityNo} 吗？", "删除", "取消");
        if (!confirm) return;

        try
        {
            var deleted = await _qualityApi.DeleteIncomingQualityOrderAsync(_incomingQualityNo);
            if (!deleted)
            {
                await DisplayAlert("删除失败", "接口未返回删除成功，请稍后重试。", "确定");
                return;
            }

            await DisplayAlert("删除成功", "来料质检单已删除。", "确定");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await DisplayAlert("删除失败", ex.Message, "确定");
        }
    }

    private async void OnBackButtonClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
