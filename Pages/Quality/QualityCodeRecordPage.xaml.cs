using System.Globalization;
using JXHLJSApp.Models.Quality;
using JXHLJSApp.Services;
using Microsoft.Maui.Controls.Shapes;

namespace JXHLJSApp.Pages.Quality;

public partial class QualityCodeRecordPage : ContentPage
{
    private const int PageSize = 20;
    private readonly IQualityCodeConvertApi _api;
    private readonly List<QualityCodeRecordDto> _records = new();
    private readonly HashSet<string> _cancelledCodes = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _navigationCts = new();
    private QualityCodeRecordDto? _pendingCancel;
    private int _pageNo;
    private bool _hasMore;
    private bool _busy;
    private bool _started;
    private bool _leaving;
    private bool _retryReset;

    public QualityCodeRecordPage(IQualityCodeConvertApi api)
    {
        InitializeComponent();
        _api = api;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_started) return;
        _started = true;
        await LoadAsync(true);
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        if (!Navigation.ModalStack.Any()) _navigationCts.Cancel();
    }

    private async Task LoadAsync(bool reset)
    {
        if (_busy || _pendingCancel is not null) return;
        _busy = true;
        LoadMoreButton.IsEnabled = RetryButton.IsEnabled = false;
        RefreshContainer.IsRefreshing = true;
        try
        {
            var nextPage = reset ? 1 : _pageNo + 1;
            var page = await _api.GetRecordsAsync(nextPage, PageSize, _navigationCts.Token);
            _navigationCts.Token.ThrowIfCancellationRequested();
            if (reset) _records.Clear();
            var records = page.records ?? new List<QualityCodeRecordDto>();
            foreach (var record in records)
            {
                if (!string.IsNullOrWhiteSpace(record.id) && _records.Any(x => x.id == record.id)) continue;
                _records.Add(record);
            }
            _pageNo = nextPage;
            _hasMore = records.Count > 0 && (page.total.HasValue
                ? (long)nextPage * PageSize < page.total.Value
                : records.Count >= PageSize);
            NoticeLabel.IsVisible = RetryButton.IsVisible = false;
            RenderRecords();
        }
        catch (OperationCanceledException) when (_navigationCts.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _retryReset = reset;
            NoticeLabel.Text = $"加载失败：{ex.Message}";
            NoticeLabel.IsVisible = RetryButton.IsVisible = true;
            // Existing cards remain visible when refresh or pagination fails.
        }
        finally
        {
            _busy = false;
            RefreshContainer.IsRefreshing = false;
            LoadMoreButton.IsEnabled = RetryButton.IsEnabled = true;
        }
    }

    private void RenderRecords()
    {
        RecordsStack.Children.Clear();
        foreach (var record in _records) RecordsStack.Children.Add(CreateCard(record));
        EmptyLabel.IsVisible = _records.Count == 0;
        LoadMoreButton.IsVisible = _hasMore;
    }

    private View CreateCard(QualityCodeRecordDto record)
    {
        var cancelled = IsCancelled(record);
        var status = !string.IsNullOrWhiteSpace(record.statusName) ? record.statusName : cancelled ? "已取消" : "已转码";
        var header = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 10 };
        header.Add(Text(Value(record.materialName), 18, "#06234B"));
        header.Add(Text(status, 13, cancelled ? "#8A9BB4" : "#07988A"), 1);
        var body = new VerticalStackLayout { Spacing = 10 };
        body.Children.Add(header);
        body.Children.Add(Text($"原码：{Value(record.sourceQrCode)}", 14));
        body.Children.Add(Text($"质检码：{Value(record.targetQrCode)}", 14));
        var metadata = $"{FormatTime(record.transTime)} · {Value(record.creator)}";
        if (cancelled) metadata += $" · {FormatTime(record.cancelTime)} 已取消";
        body.Children.Add(Text(metadata, 12, "#8A9BB4"));
        if (!cancelled && record.canCancel == true && !string.IsNullOrWhiteSpace(record.id))
        {
            var button = new Button
            {
                Text = "取消这条转码", TextColor = Color.FromArgb("#D64E40"), BackgroundColor = Colors.White,
                BorderColor = Color.FromArgb("#F3B8B0"), BorderWidth = 1, CornerRadius = 10,
                MinimumHeightRequest = 48, FontSize = 16, Margin = new Thickness(0, 6, 0, 0)
            };
            button.Clicked += (_, _) => ShowConfirmation(record);
            body.Children.Add(button);
        }
        else if (!cancelled) body.Children.Add(Text("当前不可取消", 12, "#8A9BB4"));
        return new Border
        {
            BackgroundColor = Colors.White, StrokeThickness = 0, Padding = 18,
            StrokeShape = new RoundRectangle { CornerRadius = 16 }, Content = body
        };
    }

    private static Label Text(string text, double size, string color = "#6D87AD") => new()
    {
        Text = text, FontSize = size, TextColor = Color.FromArgb(color), LineBreakMode = LineBreakMode.WordWrap,
        VerticalOptions = LayoutOptions.Center
    };

    private static bool IsCancelled(QualityCodeRecordDto record) =>
        !string.IsNullOrWhiteSpace(record.cancelTime) || record.statusName?.Contains("取消") == true ||
        record.status?.Contains("cancel", StringComparison.OrdinalIgnoreCase) == true;

    private static string Value(string? value) => string.IsNullOrWhiteSpace(value) ? "--" : value;

    private static string FormatTime(string? value)
    {
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) return Value(value);
        return time.Date == DateTime.Today ? $"今天 {time:HH:mm}" : time.ToString("MM-dd HH:mm");
    }

    private void ShowConfirmation(QualityCodeRecordDto record)
    {
        if (_busy || _pendingCancel is not null || record.canCancel != true || IsCancelled(record) || string.IsNullOrWhiteSpace(record.id)) return;
        _pendingCancel = record;
        CancelMaterialName.Text = Value(record.materialName);
        CancelQualityCode.Text = $"质检码：{Value(record.targetQrCode)}";
        RefreshContainer.IsVisible = false;
        ConfirmationScroll.IsVisible = true;
    }

    private void CloseConfirmation()
    {
        _pendingCancel = null;
        RefreshContainer.IsVisible = true;
        ConfirmationScroll.IsVisible = false;
    }

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        if (_busy || _pendingCancel is null) return;
        var record = _pendingCancel;
        _busy = true;
        KeepButton.IsEnabled = ConfirmButton.IsEnabled = false;
        CancelLoading.IsVisible = CancelLoading.IsRunning = true;
        try
        {
            await _api.CancelByIdAsync(record.id!, _navigationCts.Token);
            if (!string.IsNullOrWhiteSpace(record.targetQrCode)) _cancelledCodes.Add(record.targetQrCode);
            record.canCancel = false;
            record.statusName = "已取消";
            record.cancelTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            CloseConfirmation();
            RenderRecords();
            NoticeLabel.Text = "已取消，可以重新扫码";
            NoticeLabel.IsVisible = true;
        }
        catch (OperationCanceledException) when (_navigationCts.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!_navigationCts.IsCancellationRequested)
                await ErrorDialogService.ShowAsync(this, "取消转码失败", ex.Message, "确定");
        }
        finally
        {
            _busy = false;
            KeepButton.IsEnabled = ConfirmButton.IsEnabled = true;
            CancelLoading.IsVisible = CancelLoading.IsRunning = false;
        }
    }

    private async Task BackAsync()
    {
        if (_busy || _leaving) return;
        if (_pendingCancel is not null) { CloseConfirmation(); return; }
        _leaving = true;
        try
        {
            await Shell.Current.GoToAsync("..", new Dictionary<string, object>
            {
                ["cancelledQualityCodes"] = _cancelledCodes.ToArray()
            });
        }
        finally { _leaving = false; }
    }

    protected override bool OnBackButtonPressed()
    {
        _ = BackAsync();
        return true;
    }

    private async void OnBackClicked(object sender, EventArgs e) => await BackAsync();
    private void OnKeepClicked(object sender, EventArgs e) { if (!_busy) CloseConfirmation(); }
    private async void OnRefreshing(object sender, EventArgs e) => await LoadAsync(true);
    private async void OnLoadMoreClicked(object sender, EventArgs e) => await LoadAsync(false);
    private async void OnRetryClicked(object sender, EventArgs e) => await LoadAsync(_retryReset);
}
