namespace TallerControl.Application.Reports;

/// <summary>Un día del reporte: cobros de órdenes y ventas de mostrador, en la fecha local del taller.</summary>
public sealed record RevenueReportRow(DateOnly Date, decimal Amount, int PaymentCount);

/// <summary>Cuánto entró por cada origen: método de pago de las órdenes (<c>cash</c>, <c>transfer</c>…) o <c>counter_sale</c>.</summary>
public sealed record RevenueBreakdownRow(string Source, decimal Amount, int Count);

public sealed record RevenueReportDto(
    IReadOnlyList<RevenueReportRow> Rows,
    decimal Total,
    int Count,
    IReadOnlyList<RevenueBreakdownRow>? Breakdown = null);

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
