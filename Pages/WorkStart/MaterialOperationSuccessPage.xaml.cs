namespace JXHLJSApp.Pages.WorkStart;

public partial class MaterialOperationSuccessPage : ContentPage, IQueryAttributable
{
    private bool _returnThroughUnloadingFlow;

    public MaterialOperationSuccessPage()
    {
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _returnThroughUnloadingFlow = query.TryGetValue("operation", out var operation)
            && string.Equals(operation?.ToString(), "unloading", StringComparison.OrdinalIgnoreCase);
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await GoBackToExecutionAsync();

    private async void OnDoneClicked(object sender, EventArgs e) => await GoBackToExecutionAsync();

    private Task GoBackToExecutionAsync() => Shell.Current.GoToAsync(
        _returnThroughUnloadingFlow ? "../../.." : "../..");
}
