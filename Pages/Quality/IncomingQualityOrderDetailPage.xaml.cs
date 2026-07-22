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
    private readonly IScanService _scanService;
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
        _scanService = scanService;
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
            TotalLabel.Text = _detail.totalDisplay;
            RenderMaterialDetails(_detail);
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
            await ErrorDialogService.ShowAsync(this, "加载失败", ex.Message, "确定");
        }
        finally
        {
            RefreshContainer.IsRefreshing = false;
        }
    }

    private void RenderMaterialDetails(IncomingQualityOrderDetailDto detail)
    {
        MaterialDetailList.Children.Clear();
        MaterialDetailTitle.Text = $"物料明细（{detail.materialDetailCount}项）";
        foreach (var item in detail.materialDetails)
        {
            MaterialDetailList.Add(CreateMaterialDetailRow(item));
        }
    }

    private static Border CreateMaterialDetailRow(IncomingQualityMaterialDetailDto detail)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };

        var materialLabel = new Label
        {
            Text = detail.materialSpecDisplay,
            TextColor = Color.FromArgb("#38557C"),
            FontSize = 13,
            LineBreakMode = LineBreakMode.TailTruncation
        };

        var quantityLabel = new Label
        {
            Text = detail.countDisplay,
            TextColor = Color.FromArgb("#051B3D"),
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.End
        };

        grid.Add(materialLabel, 0);
        grid.Add(quantityLabel, 1);

        return new Border
        {
            BackgroundColor = Color.FromArgb("#F8FAFD"),
            Stroke = Color.FromArgb("#DDE7F2"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            Padding = new Thickness(10, 8),
            Content = grid
        };
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
        var code = await _scanService.ScanAsync("来料质检扫码");
        if (string.IsNullOrWhiteSpace(code)) return;

        try
        {
            var qrCode = code.Trim();
            var scanMaterial = await _qualityApi.ScanIncomingQualityMaterialAsync(qrCode);
            var parameters = new Dictionary<string, object>
            {
                ["qrCode"] = qrCode,
                ["scanMaterial"] = scanMaterial
            };

            if (!string.IsNullOrWhiteSpace(_incomingQualityNo))
            {
                parameters["incomingQualityNo"] = _incomingQualityNo;
            }

            await Shell.Current.GoToAsync(AppShell.RouteIncomingQualityScan, parameters);
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "扫码失败", ex.Message, "确定");
        }
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
                await ErrorDialogService.ShowAsync(this, "完成失败", "接口未返回完成成功，请稍后重试。", "确定");
                return;
            }
            await DisplayAlert("完成成功", "来料质检单已完成。", "确定");
            await LoadDetailAsync();
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "完成失败", ex.Message, "确定");
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
                await ErrorDialogService.ShowAsync(this, "删除失败", "接口未返回删除成功，请稍后重试。", "确定");
                return;
            }

            await DisplayAlert("删除成功", "来料质检单已删除。", "确定");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await ErrorDialogService.ShowAsync(this, "删除失败", ex.Message, "确定");
        }
    }

    private async void OnBackButtonClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
    private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
