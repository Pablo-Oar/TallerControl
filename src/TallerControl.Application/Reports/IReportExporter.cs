namespace TallerControl.Application.Reports;

public interface IReportExporter
{
    byte[] ExportRevenue(RevenueReportDto report);
    byte[] ExportReceivables(ReceivablesReportDto report);
    byte[] ExportStock(StockReportDto report);
    byte[] ExportPurchases(PurchasesReportDto report);
}
