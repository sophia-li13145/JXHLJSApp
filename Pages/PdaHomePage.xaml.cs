using Microsoft.Maui.Controls.Shapes;

namespace JXHLJSApp.Pages;

public partial class PdaHomePage : ContentPage
{
    private static readonly IReadOnlyDictionary<string, ModuleDefinition> Modules =
        new Dictionary<string, ModuleDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["production"] = new("生产管理", "wrench_24_filled.svg", "#1765EF", "#DCE9FF"),
            ["qualityInspector"] = new("质检管理", "beaker_24_filled.svg", "#8A24DE", "#EEDCFF"),
            ["warehouseKeeper"] = new("仓库管理", "box_24_filled.svg", "#F06A16", "#FFE5D3"),
            ["forkliftOperator"] = new("叉车工", "vehicle_truck_24_filled.svg", "#009D88", "#D2F3EC")
        };

    public PdaHomePage()
    {
        InitializeComponent();
        BuildModules();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        BuildModules();
    }

    private void BuildModules()
    {
        ModuleGrid.Children.Clear();
        ModuleGrid.RowDefinitions.Clear();
        var roleCodes = UserRoleAccess.GetStoredVisibleRoleCodes();

        for (var index = 0; index < roleCodes.Count; index++)
        {
            if (index % 2 == 0) ModuleGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(110) });
            if (!Modules.TryGetValue(roleCodes[index], out var module)) continue;

            var icon = new Border
            {
                WidthRequest = 52,
                HeightRequest = 52,
                BackgroundColor = Color.FromArgb(module.IconBackground),
                Stroke = Color.FromArgb(module.IconBorderColor),
                StrokeThickness = 5,
                StrokeShape = new RoundRectangle { CornerRadius = 26 },
                Content = new Image
                {
                    Source = module.IconSource,
                    WidthRequest = 25,
                    HeightRequest = 25,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };
            var card = new Border
            {
                BackgroundColor = Colors.White,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Shadow = new Shadow { Brush = Color.FromArgb("#16000000"), Offset = new Point(0, 3), Radius = 9 },
                Content = new VerticalStackLayout
                {
                    Spacing = 8,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Children = { icon, new Label { Text = module.Title, TextColor = Color.FromArgb("#071A38"), FontSize = 15, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center } }
                }
            };
            var routeRoleCode = roleCodes[index];
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await Shell.Current.GoToAsync($"{AppShell.RouteRoleModule}?roleCode={Uri.EscapeDataString(routeRoleCode)}");
            card.GestureRecognizers.Add(tap);
            ModuleGrid.Add(card, index % 2, index / 2);
        }
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        await TokenStorage.ClearAsync();
        UserSessionStore.Clear();
        App.SwitchToLoggedOutShell();
    }

    private sealed record ModuleDefinition(string Title, string IconSource, string IconBackground, string IconBorderColor);
}
