using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Dashboard;
using TallerControl.Domain.Catalog;
using TallerControl.Domain.Quotes;
using TallerControl.Domain.Work;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Dashboard;

internal sealed class EfDashboardRepository : IDashboardRepository
{
    private readonly AppDbContext _db;

    public EfDashboardRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardDto> GetSummaryAsync(CancellationToken ct)
    {
        // La columna generada `balance` ya retorna 0 para las OTs canceladas.
        var totalReceivable = await _db.Set<WorkOrder>()
            .AsNoTracking()
            .SumAsync(w => EF.Property<decimal>(w, "Balance"), ct);

        var openWorkOrders = await _db.Set<WorkOrder>()
            .AsNoTracking()
            .Where(w => w.Status != WorkOrderStatus.Completed
                     && w.Status != WorkOrderStatus.Delivered
                     && w.Status != WorkOrderStatus.Cancelled)
            .CountAsync(ct);

        var pendingQuotes = await _db.Set<Quote>()
            .AsNoTracking()
            .Where(q => q.Status == QuoteStatus.Draft || q.Status == QuoteStatus.Sent)
            .CountAsync(ct);

        var lowStockItems = await _db.Set<CatalogItem>()
            .AsNoTracking()
            .Where(c => c.TrackStock && c.StockMin.HasValue && c.StockQty < c.StockMin.Value)
            .CountAsync(ct);

        return new DashboardDto(totalReceivable, openWorkOrders, pendingQuotes, lowStockItems);
    }
}
