namespace TallerControl.Application.Dashboard;

public sealed record DashboardDto(
    decimal TotalReceivable,
    int OpenWorkOrders,
    int PendingQuotes,
    int LowStockItems
);
