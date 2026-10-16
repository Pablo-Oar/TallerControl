namespace TallerControl.Application.Reports;

public interface IReportsRepository
{
    Task<RevenueReportDto> GetRevenueAsync(DateOnly from, DateOnly to, CancellationToken ct);
    Task<ReceivablesReportDto> GetReceivablesAsync(CancellationToken ct);
    Task<StockReportDto> GetStockAsync(CancellationToken ct);
    Task<PurchasesReportDto> GetPurchasesAsync(DateOnly from, DateOnly to, CancellationToken ct);
}
