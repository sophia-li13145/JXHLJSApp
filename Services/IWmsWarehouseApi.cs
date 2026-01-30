using IndustrialControlMAUI.Services.Common;

using IndustrialControlMAUI.Models;
using IndustrialControlMAUI.Tools;
using Serilog;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace IndustrialControlMAUI.Services;

public interface IWarehouseService
{
    Task<IReadOnlyList<WarehouseItem>> QueryAllWarehouseAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LocationItem>> QueryLocationsByWarehouseCodeAsync(string warehouseCode, CancellationToken ct = default);
    Task<IReadOnlyList<LocationSegment>> QueryLocationSegmentsByWarehouseCodeAsync(string warehouseCode, CancellationToken ct = default);
}
