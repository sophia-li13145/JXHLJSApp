namespace JXHLJSApp.Pages.Warehouse;

public partial class MaterialReceivingSummaryView : ContentView
{
    public MaterialReceivingSummaryView()
    {
        InitializeComponent();
    }

    internal void Show(string title, IReadOnlyList<MaterialSummaryItem> items)
    {
        SummaryTitle.Text = title;
        SummaryList.ItemsSource = items;
        SummaryTotalCount.Text = $"{items.Sum(item => item.count)} 件";
        var weightUnit = items[0].weightUnit;
        var hasMixedWeightUnits = items.Any(item => item.weightUnit != weightUnit);
        var totalWeight = hasMixedWeightUnits
            ? items.Sum(item => item.weightUnit == "KG" ? item.totalWeight / 1000m : item.totalWeight)
            : items.Sum(item => item.totalWeight);
        SummaryTotalWeight.Text = $"{totalWeight:F3} {(hasMixedWeightUnits ? "吨" : weightUnit)}";
        IsVisible = true;
    }

    private void OnCloseSummaryTapped(object sender, TappedEventArgs e) => IsVisible = false;

    private void OnCloseSummaryClicked(object sender, EventArgs e) => IsVisible = false;
}
