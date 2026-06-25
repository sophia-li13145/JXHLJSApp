using JXHLJSApp.ViewModels;

namespace JXHLJSApp.Pages;

public partial class LogPage : ContentPage
{
    private readonly LogsViewModel _viewModel;

    public LogPage(LogsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.RefreshCommand.Execute(null);
    }
}
