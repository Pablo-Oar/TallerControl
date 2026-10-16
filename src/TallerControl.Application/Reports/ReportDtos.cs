namespace TallerControl.Application.Reports;

public sealed record RevenueReportRow(DateOnly Date, decimal Amount, int PaymentCount);
public sealed record RevenueReportDto(IReadOnlyList<RevenueReportRow> Rows, decimal Total, int Count);

public sealed record ReceivablesReportRow(
    string WorkOrderId, int WorkOrderNumber, string CustomerName,
    string VehiclePlate, decimal Balance, DateTimeOffset CreatedAt);
public sealed record ReceivablesReportDto(IReadOnlyList<ReceivablesReportRow> Rows, decimal Total);

public sealed record StockReportRow(
    string CatalogItemId, string Name, string? Sku, string Kind,
    decimal StockQty, decimal? StockMin, decimal UnitCost, bool IsLowStock);
public sealed record StockReportDto(IReadOnlyList<StockReportRow> Rows, int LowStockCount);

public sealed record PurchasesReportRow(
    string PurchaseId, int PurchaseNumber, string? SupplierName,
    DateOnly Date, decimal Total, string Status);
public sealed record PurchasesReportDto(IReadOnlyList<PurchasesReportRow> Rows, decimal Total, int Count);
