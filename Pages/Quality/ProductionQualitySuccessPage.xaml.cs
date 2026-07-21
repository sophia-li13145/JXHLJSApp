namespace JXHLJSApp.Pages.Quality;

public partial class ProductionQualitySuccessPage : ContentPage
{
    public ProductionQualitySuccessPage()
    {
        InitializeComponent();
    }

    private async void OnReturnClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("../..");
}
