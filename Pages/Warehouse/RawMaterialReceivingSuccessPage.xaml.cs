namespace JXHLJSApp.Pages.Warehouse;

[QueryProperty(nameof(WarehouseName), nameof(WarehouseName))]
[QueryProperty(nameof(WarehouseArea), nameof(WarehouseArea))]
public partial class RawMaterialReceivingSuccessPage : ContentPage
{
    private string? _warehouseName;
    private string? _warehouseArea;

    public string? WarehouseName
    {
        get => _warehouseName;
        set
        {
            _warehouseName = DecodeQueryValue(value);
            UpdateSuccessMessage();
        }
    }

    public string? WarehouseArea
    {
        get => _warehouseArea;
        set
        {
            _warehouseArea = DecodeQueryValue(value);
            UpdateSuccessMessage();
        }
    }

    public RawMaterialReceivingSuccessPage()
    {
        InitializeComponent();
        UpdateSuccessMessage();
    }

    private void UpdateSuccessMessage()
    {
        if (SuccessMessageLabel is null)
        {
            return;
        }

        SuccessMessageLabel.Text = $"数据已录入至 {FirstNonEmpty(_warehouseName)} - {FirstNonEmpty(_warehouseArea)}，当前状态：待质检";
    }

    private async void OnReturnTapped(object sender, TappedEventArgs e) => await ReturnToReceivingListAsync();

    private async void OnReturnClicked(object sender, EventArgs e) => await ReturnToReceivingListAsync();

    private static Task ReturnToReceivingListAsync() => Shell.Current.GoToAsync("../..");

    private static string DecodeQueryValue(string? value) => Uri.UnescapeDataString(value ?? string.Empty);

    private static string FirstNonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? "--" : value.Trim();
}
