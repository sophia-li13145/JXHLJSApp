using JXHLJSApp.Services.Common;
using JXHLJSApp.Models;
using JXHLJSApp.Tools;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace JXHLJSApp.Services
{
    // ===================== 接口定义 =====================
    public interface IMoldApi
    {
        Task<MoldOutScanQueryResp?> InStockScanQueryAsync(
   string code, CancellationToken ct = default);
        Task<SimpleOk> ConfirmInStockByListAsync(InStockConfirmReq req, CancellationToken ct = default);

        //-------------------------------出库-------------------------------------------
        /// <summary>
        /// 返回页面所需：工单号、物料名称、以及“型号+基础需求数量+模具编码列表”的分组视图
        /// </summary>
        Task<WorkOrderMoldView> GetViewAsync(string workOrderNo, CancellationToken ct = default);

        /// <summary>出库扫描查询</summary>
        Task<MoldOutScanQueryResp?> OutStockScanQueryAsync(string code, string workOrderNo, CancellationToken ct = default);

        /// <summary>确认出库</summary>
        Task<SimpleOk> ConfirmOutStockAsync(MoldOutConfirmReq req, CancellationToken ct = default);
    }
}
