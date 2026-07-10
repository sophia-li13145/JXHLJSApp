using JXHLJSApp.Models.Quality;

namespace JXHLJSApp.Pages.Quality;

internal static class MachineQualityTaskStore
{
    private static string? _resourceCode;
    private static List<ProductionQualityOrderDto> _tasks = new();

    public static void Save(string resourceCode, List<ProductionQualityOrderDto> tasks)
    {
        _resourceCode = resourceCode;
        _tasks = tasks;
    }

    public static IReadOnlyList<ProductionQualityOrderDto> GetTasks(string? resourceCode)
    {
        if (!string.Equals(_resourceCode, resourceCode, StringComparison.Ordinal)) return Array.Empty<ProductionQualityOrderDto>();
        return _tasks;
    }
}
