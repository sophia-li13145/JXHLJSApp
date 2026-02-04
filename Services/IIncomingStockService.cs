using JXHLJSApp.Models;

namespace JXHLJSApp.Services;

public interface IIncomingStockService
{
    Task<IncomingBarcodeParseResponse?> ParseIncomingBarcodeAsync(string barcode, CancellationToken ct = default);
    Task<SimpleOk> SubmitPendingStockAsync(IEnumerable<IncomingPendingStockRequest> items, CancellationToken ct = default);
}
