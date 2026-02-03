using JXHLJSApp.Services.Common;
using AndroidX.Annotations;
using JXHLJSApp.Models;
using JXHLJSApp.Tools;
using System.Net.Http.Headers;
using System.Text.Json;

namespace JXHLJSApp.Services
{
    // ===================== 接口定义 =====================
    public interface IEnergyApi
    {
        /// <summary>查询仪表分页（图1）</summary>
        Task<PageResponeResult<MeterRecordDto>> MeterPageQueryAsync(
            int pageNo,
            int pageSize,
            string? meterCode,
            string? energyType,   // electric/water/gas/compressed_air，null 表示全部
            string? workshopId,
            string? lineId,
            bool searchCount,
            CancellationToken ct = default);

        /// <summary>能源类型字典（electric/water/gas/compressed_air → 显示名）</summary>
        Task<List<EnergyDictItem>> GetEnergyTypeDictAsync(CancellationToken ct = default);

        /// <summary>车间列表（默认 workshopsType=workshop）</summary>
        Task<List<IdNameOption>> GetWorkshopsAsync(string? workshopsType = "workshop", CancellationToken ct = default);

        /// <summary>用户列表（抄表人候选）</summary>
        Task<List<IdNameOption>> GetUsersAsync(CancellationToken ct = default);

        Task<List<IdNameOption>> GetProductLinesAsync(string? workshopsType = "production_line", CancellationToken ct = default);

        Task<List<MeterPointItem>> GetMeterPointsByMeterCodeAsync(string meterCode, CancellationToken ct = default);

        Task<LastReadingResult?> GetLastReadingAsync(string meterCode, string meterPointCode, CancellationToken ct = default);

        Task<bool> SaveMeterReadingAsync(MeterSaveReq req, CancellationToken ct = default);

        Task<List<DevItem>> GetDevListAsync(CancellationToken ct = default);
    }
}
