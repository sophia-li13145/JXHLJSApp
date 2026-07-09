using JXHLJSApp.Models;
using JXHLJSApp.Services;
using JXHLJSApp.Services.WorkOrders;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace JXHLJSApp.Pages;

public partial class RoleHomePage : ContentPage
{
    private readonly IWorkOrderApi _workOrderApi;
    private readonly IScanService _scanService;

    public RoleHomePage(IWorkOrderApi workOrderApi, IScanService scanService)
    {
        _workOrderApi = workOrderApi;
        _scanService = scanService;
        InitializeComponent();
        BuildRoleHome();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        BuildRoleHome();
    }

    private void BuildRoleHome()
    {
        var roleCode = Preferences.Get(UserSessionKeys.RoleCode, string.Empty);
        var realName = Preferences.Get(UserSessionKeys.RealName, "未命名");
        var workNumber = Preferences.Get(UserSessionKeys.WorkNumber, string.Empty);
        var department = Preferences.Get(UserSessionKeys.DepartmentName, string.Empty);
        var team = Preferences.Get(UserSessionKeys.TeamName, string.Empty);
        var shift = Preferences.Get(UserSessionKeys.ShiftName, string.Empty);

        var role = RoleHomeDefinition.FromRoleCode(roleCode);
        TitleLabel.Text = role.Title;
        ContentStack.Children.Clear();
        ContentStack.Children.Add(CreateProfileCard(role, realName, workNumber, department, team, shift));

        if (role.RoleCode == "production")
        {
            ContentStack.Children.Add(CreateMachineBindCard());
        }

        if (!string.IsNullOrWhiteSpace(role.SectionTitle))
        {
            ContentStack.Children.Add(new Label
            {
                Text = role.SectionTitle,
                TextColor = Color.FromArgb("#051B3D"),
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                Margin = new Thickness(0, 10, 0, 0)
            });
        }

        if (role.Layout == RoleHomeLayout.Grid)
        {
            ContentStack.Children.Add(CreateModuleGrid(role.Modules));
        }
        else
        {
            foreach (var module in role.Modules)
            {
                ContentStack.Children.Add(CreateWideModule(module));
            }
        }
    }

    private static View CreateProfileCard(RoleHomeDefinition role, string realName, string workNumber, string department, string team, string shift)
    {
        var tag1 = role.ProfileLabel(realName, workNumber, department);
        var tag2 = role.SecondaryProfileLabel(shift, team);

        var details = new VerticalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center };
        details.Children.Add(new Label
        {
            Text = realName,
            TextColor = Colors.White,
            FontSize = 26,
            FontAttributes = FontAttributes.Bold
        });

        var chips = new FlexLayout { Wrap = FlexWrap.Wrap, Direction = FlexDirection.Row, AlignItems = FlexAlignItems.Start };
        AddChip(chips, tag1);
        AddChip(chips, tag2);
        details.Children.Add(chips);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition { Width = GridLength.Star }, new ColumnDefinition { Width = GridLength.Auto }) };
        grid.Add(details);
        grid.Add(new Label
        {
            Text = role.AvatarIcon,
            FontSize = 42,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End
        }, 1);

        return new Border
        {
            BackgroundColor = Color.FromArgb(role.ProfileBackground),
            StrokeThickness = 0,
            Padding = new Thickness(24, 20),
            HeightRequest = 124,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Shadow = new Shadow { Brush = Brush.Black, Opacity = 0.2f, Offset = new Point(0, 6), Radius = 10 },
            Content = grid
        };
    }

    private static void AddChip(FlexLayout chips, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        chips.Children.Add(new Border
        {
            BackgroundColor = Color.FromArgb("#33FFFFFF"),
            StrokeThickness = 0,
            Padding = new Thickness(12, 5),
            Margin = new Thickness(0, 0, 6, 6),
            StrokeShape = new RoundRectangle { CornerRadius = 7 },
            Content = new Label { Text = text, TextColor = Colors.White, FontSize = 13 }
        });
    }

    private static View CreateModuleGrid(IReadOnlyList<HomeModule> modules)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition { Width = GridLength.Star }, new ColumnDefinition { Width = GridLength.Star }),
            RowSpacing = 16,
            ColumnSpacing = 16
        };

        for (var i = 0; i < modules.Count; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var i = 0; i < modules.Count; i++)
        {
            grid.Add(CreateSquareModule(modules[i]), i % 2, i / 2);
        }

        return grid;
    }

    private static View CreateSquareModule(HomeModule module)
    {
        var card = new Border
        {
            BackgroundColor = Colors.White,
            StrokeThickness = 0,
            HeightRequest = 138,
            Padding = new Thickness(10),
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Shadow = new Shadow { Brush = Color.FromArgb("#22000000"), Offset = new Point(0, 3), Radius = 8 },
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    CreateIconBadge(module),
                    new Label { Text = module.Title, TextColor = Color.FromArgb("#061A3B"), FontSize = 16, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center }
                }
            }
        };

        AttachNavigation(card, module);
        return card;
    }

    private static View CreateWideModule(HomeModule module)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition { Width = new GridLength(92) }, new ColumnDefinition { Width = GridLength.Star }), ColumnSpacing = 18 };
        grid.Add(CreateIconBadge(module, 72, 34));
        grid.Add(new VerticalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = module.Title, TextColor = Color.FromArgb("#061A3B"), FontSize = 18, FontAttributes = FontAttributes.Bold },
                new Label { Text = module.Description, TextColor = Color.FromArgb("#4B6688"), FontSize = 13 }
            }
        }, 1);

        var card = new Border
        {
            BackgroundColor = module.Highlight ? Color.FromArgb("#EAF4FF") : Colors.White,
            Stroke = module.Highlight ? Color.FromArgb("#BBD8FF") : Colors.Transparent,
            StrokeThickness = module.Highlight ? 1 : 0,
            Padding = new Thickness(20, 16),
            HeightRequest = 112,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Shadow = module.Highlight ? null : new Shadow { Brush = Color.FromArgb("#18000000"), Offset = new Point(0, 3), Radius = 8 },
            Content = grid
        };

        AttachNavigation(card, module);
        return card;
    }

    private static void AttachNavigation(View view, HomeModule module)
    {
        if (module.Route is null) return;

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await Shell.Current.GoToAsync(module.Route);
        view.GestureRecognizers.Add(tap);
    }

    private static View CreateIconBadge(HomeModule module, double size = 58, double iconSize = 28)
    {
        return new Border
        {
            WidthRequest = size,
            HeightRequest = size,
            BackgroundColor = module.IconBackground,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(size / 2) },
            Content = new Label
            {
                Text = module.IconGlyph,
                FontSize = iconSize,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };
    }


    private View CreateMachineBindCard()
    {
        var entry = new Entry
        {
            Placeholder = "或手动输入机台编号",
            PlaceholderColor = Color.FromArgb("#7A8797"),
            FontSize = 14,
            BackgroundColor = Color.FromArgb("#F8FAFD"),
            HeightRequest = 46
        };

        var scanButton = new Button
        {
            Text = "📷  扫码机台二维码上机",
            BackgroundColor = Color.FromArgb("#1F447E"),
            TextColor = Colors.White,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 10,
            HeightRequest = 56
        };
        scanButton.Clicked += async (_, _) =>
        {
            var code = await _scanService.ScanAsync("扫码机台二维码上机");
            if (!string.IsNullOrWhiteSpace(code))
            {
                await BindMachineAndOpenOrdersAsync(code);
            }
        };

        var confirmButton = new Button
        {
            Text = "确认",
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#0B3D8B"),
            BorderColor = Color.FromArgb("#C8D6EA"),
            BorderWidth = 1,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 10,
            HeightRequest = 46
        };
        confirmButton.Clicked += async (_, _) => await BindMachineAndOpenOrdersAsync(entry.Text);

        var inputGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition { Width = GridLength.Star }, new ColumnDefinition { Width = new GridLength(72) }),
            ColumnSpacing = 10
        };
        inputGrid.Add(entry);
        inputGrid.Add(confirmButton, 1);

        return new Border
        {
            BackgroundColor = Colors.White,
            StrokeThickness = 0,
            Padding = new Thickness(20),
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Content = new VerticalStackLayout
            {
                Spacing = 16,
                Children = { scanButton, inputGrid }
            }
        };
    }

    private async Task BindMachineAndOpenOrdersAsync(string? machineCode)
    {
        var devCode = machineCode?.Trim();
        if (string.IsNullOrWhiteSpace(devCode))
        {
            await DisplayAlert("提示", "请输入机台编号", "确定");
            return;
        }

        try
        {
            var result = await _workOrderApi.BindWorkerMachineAsync(devCode);
            if (!result)
            {
                await DisplayAlert("绑定失败", "机台绑定未成功，请确认机台编号后重试。", "确定");
                return;
            }

            await Shell.Current.GoToAsync(AppShell.RouteWorkStartOrders);
        }
        catch (Exception ex)
        {
            await DisplayAlert("绑定失败", ex.Message, "确定");
        }
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        await TokenStorage.ClearAsync();
        UserSessionStore.Clear();
        App.SwitchToLoggedOutShell();
    }
}

internal static class UserSessionKeys
{
    public const string RoleCode = "RoleCode";
    public const string RealName = "RealName";
    public const string WorkNumber = "WorkNumber";
    public const string DepartmentName = "DepartmentName";
    public const string TeamName = "TeamName";
    public const string ShiftName = "ShiftName";
}

internal static class UserSessionStore
{
    public static void Save(UserInfoDto? userInfo)
    {
        Preferences.Set(UserSessionKeys.RoleCode, userInfo?.roleCode ?? string.Empty);
        Preferences.Set(UserSessionKeys.RealName, FirstNonEmpty(userInfo?.realname, userInfo?.username, "未命名"));
        Preferences.Set(UserSessionKeys.WorkNumber, FirstNonEmpty(userInfo?.workNumber, userInfo?.id, string.Empty));
        Preferences.Set(UserSessionKeys.DepartmentName, FirstNonEmpty(userInfo?.workshopName, userInfo?.factoryName, string.Empty));
        Preferences.Set(UserSessionKeys.TeamName, FirstNonEmpty(userInfo?.teamName, userInfo?.roleName, string.Empty));
        Preferences.Set(UserSessionKeys.ShiftName, FirstNonEmpty(userInfo?.shiftName, userInfo?.loginType, string.Empty));
    }

    public static void Clear()
    {
        Preferences.Remove(UserSessionKeys.RoleCode);
        Preferences.Remove(UserSessionKeys.RealName);
        Preferences.Remove(UserSessionKeys.WorkNumber);
        Preferences.Remove(UserSessionKeys.DepartmentName);
        Preferences.Remove(UserSessionKeys.TeamName);
        Preferences.Remove(UserSessionKeys.ShiftName);
    }

    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}

internal enum RoleHomeLayout { Grid, List }

internal sealed record HomeModule(string Title, string IconGlyph, Color IconBackground, string Description = "", bool Highlight = false, string? Route = null);

internal sealed record RoleHomeDefinition(
    string RoleCode,
    string Title,
    string ProfileBackground,
    string AvatarIcon,
    RoleHomeLayout Layout,
    IReadOnlyList<HomeModule> Modules,
    string SectionTitle = "")
{
    public static RoleHomeDefinition FromRoleCode(string? roleCode)
    {
        return (roleCode ?? string.Empty).Trim() switch
        {
            "warehouseKeeper" => new("warehouseKeeper", "仓储与包装管理", "#159957", "📦", RoleHomeLayout.Grid, new[]
            {
                new HomeModule("采购入库", "📥", Color.FromArgb("#F3F0FF"), Route: AppShell.RouteRawMaterialReceiving),
                new HomeModule("发货出库", "🚚", Color.FromArgb("#EFF8F0"), Route: AppShell.RouteDeliveryOrders),
                new HomeModule("包装作业", "🎁", Color.FromArgb("#FFF5F5"), Route: AppShell.RoutePackagingSubTasks)
            }),
            "qualityInspector" => new("qualityInspector", "质检管理工作台", "#14295D", "👨‍🔬", RoleHomeLayout.List, new[]
            {
                new HomeModule("机台质检 (快捷取)", "🏭", Color.FromArgb("#DCEBFF"), "扫码机台，直达该机台质检任务", true),
                new HomeModule("生产质检记录 (PQC)", "⚙️", Color.FromArgb("#FFF1F1"), "首检 · 抽检 · 热处理 · 酸洗 任务池"),
                new HomeModule("来料质检记录 (IQC)", "📥", Color.FromArgb("#FFFBEA"), "基于入库单的原材料质检记录", Route: AppShell.RouteIncomingQualityOrders)
            }, "请选择业务模块"),
            "forkliftOperator" => new("forkliftOperator", "叉车运输工作台", "#E07700", "🚚", RoleHomeLayout.List, new[]
            {
                new HomeModule("工序间转运", "🔄", Color.FromArgb("#EAF2FF"), "扫码获取物料下道工序并转运", Route: AppShell.RouteProcessTransferScan),
                new HomeModule("出库运输查看", "🚚", Color.FromArgb("#EFF8F0"), "查看领料单及出库详情信息", Route: AppShell.RouteOutstockTransportOrders),
                new HomeModule("成品入库查看", "📥", Color.FromArgb("#F3F0FF"), "查看倒推生成的成品入库单据", Route: AppShell.RouteProductInstockTransportOrders)
            }),
            _ => new("production", "生产作业首页", "#14295D", "👷", RoleHomeLayout.Grid, new[]
            {
                new HomeModule("任务列表", "📋", Color.FromArgb("#55ACE3"), Route: AppShell.RouteWorkOrderTasks),
                new HomeModule("异常上报", "⚠️", Color.FromArgb("#F27655"), Route: AppShell.RouteAbnormalReport),
                new HomeModule("返工上报", "↩️", Color.FromArgb("#DEBC79"), Route: AppShell.RouteReworkReport),
                new HomeModule("生产统计", "📈", Color.FromArgb("#9B632A"), Route: AppShell.RouteProductionStatistics)
            })
        };
    }

    public string ProfileLabel(string realName, string workNumber, string department)
    {
        return RoleCode switch
        {
            "production" => $"🏭 {FirstNonEmpty(department, "生产车间")}   🆔 {FirstNonEmpty(workNumber, "EMP0001")}",
            "warehouseKeeper" => $"仓储部 · {FirstNonEmpty(workNumber, "EMP0301")}",
            "qualityInspector" => $"🔬 {FirstNonEmpty(department, "品管部")}   🆔 {FirstNonEmpty(workNumber, "EMP0201")}",
            "forkliftOperator" => $"叉车组 · {FirstNonEmpty(workNumber, "EMP0101")}",
            _ => FirstNonEmpty(workNumber, department)
        };
    }

    public string SecondaryProfileLabel(string shift, string team)
    {
        return RoleCode == "production" ? $"🕘 班次: {FirstNonEmpty(shift, "白班")}   👥 班组: {FirstNonEmpty(team, "甲组")}" : string.Empty;
    }

    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
