using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Reports;
using TallerControl.Domain.Catalog;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Payments;
using TallerControl.Domain.Purchases;
using TallerControl.Domain.Suppliers;
using TallerControl.Domain.Vehicles;
using TallerControl.Domain.Work;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Reports;

internal sealed class EfReportsRepository : IReportsRepository
{
    private readonly AppDbContext _db;

    public EfReportsRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<RevenueReportDto> GetRevenueAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var fromUtc = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toUtc = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        // DateOnly.FromDateTime no es traducible por Npgsql; se agrupa en memoria.
        var payments = await _db.Set<Payment>()
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Valid
                     && p.PaidAt >= fromUtc
                     && p.PaidAt <= toUtc)
            .Select(p => new { p.PaidAt, p.Amount })
            .ToListAsync(ct);

        var rows = payments
            .GroupBy(p => DateOnly.FromDateTime(p.PaidAt.UtcDateTime))
            .Select(g => new RevenueReportRow(g.Key, g.Sum(p => p.Amount), g.Count()))
            .OrderBy(r => r.Date)
            .ToList();

        return new RevenueReportDto(rows, rows.Sum(r => r.Amount), rows.Sum(r => r.PaymentCount));
    }

    public async Task<ReceivablesReportDto> GetReceivablesAsync(CancellationToken ct)
    {
        var raw = await _db.Set<WorkOrder>()
            .AsNoTracking()
            .Where(w => w.Status != WorkOrderStatus.Cancelled
                     && EF.Property<decimal>(w, "Balance") > 0)
            .Select(w => new
            {
                Id = w.Id.ToString(),
                w.Number,
                w.CustomerId,
                w.VehicleId,
                Balance = EF.Property<decimal>(w, "Balance"),
                w.CreatedAt,
            })
            .OrderByDescending(r => r.Balance)
            .ToListAsync(ct);

        if (raw.Count == 0)
            return new ReceivablesReportDto([], 0);

        var customerIds = raw.Select(r => r.CustomerId).Distinct().ToList();
        var vehicleIds = raw.Select(r => r.VehicleId).Distinct().ToList();

        var customers = await _db.Set<Customer>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var vehicles = await _db.Set<Vehicle>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(v => vehicleIds.Contains(v.Id))
            .Select(v => new { v.Id, v.Plate })
            .ToDictionaryAsync(v => v.Id, v => v.Plate, ct);

        var rows = raw.Select(r => new ReceivablesReportRow(
            r.Id,
            r.Number,
            customers.TryGetValue(r.CustomerId, out var cn) ? cn : string.Empty,
            vehicles.TryGetValue(r.VehicleId, out var plate) ? plate : string.Empty,
            r.Balance,
            r.CreatedAt)).ToList();

        return new ReceivablesReportDto(rows, rows.Sum(r => r.Balance));
    }

    public async Task<StockReportDto> GetStockAsync(CancellationToken ct)
    {
        var rows = await _db.Set<CatalogItem>()
            .AsNoTracking()
            .Where(c => c.TrackStock)
            // El orden va antes de la proyección: EF no traduce un OrderBy sobre el record ya construido.
            .OrderBy(c => c.Name)
            .Select(c => new StockReportRow(
                c.Id.ToString(),
                c.Name,
                c.Sku,
                c.Kind.ToString(),
                c.StockQty,
                c.StockMin,
                c.Cost ?? 0m,
                // IsLowStock es una propiedad calculada de C#: EF no la traduce, se repite la condición.
                c.TrackStock && (c.StockQty < 0 || (c.StockMin != null && c.StockQty < c.StockMin))))
            .ToListAsync(ct);

        return new StockReportDto(rows, rows.Count(r => r.IsLowStock));
    }

    public async Task<PurchasesReportDto> GetPurchasesAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var fromUtc = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toUtc = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        // DateOnly.FromDateTime no es traducible por Npgsql; se convierte en memoria.
        var raw = await _db.Set<Purchase>()
            .AsNoTracking()
            .Where(p => p.CreatedAt >= fromUtc && p.CreatedAt <= toUtc
                     && p.Status != PurchaseStatus.Cancelled)
            .Select(p => new
            {
                Id = p.Id.ToString(),
                Number = p.DocumentNumber,
                p.SupplierId,
                p.CreatedAt,
                p.Total,
                Status = p.Status.ToString(),
            })
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        if (raw.Count == 0)
            return new PurchasesReportDto([], 0m, 0);

        var supplierIds = raw.Select(r => r.SupplierId).Distinct().ToList();

        var suppliers = await _db.Set<Supplier>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => supplierIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        var rows = raw.Select(r => new PurchasesReportRow(
            r.Id,
            r.Number,
            suppliers.TryGetValue(r.SupplierId, out var sn) ? sn : null,
            DateOnly.FromDateTime(r.CreatedAt.UtcDateTime),
            r.Total,
            r.Status)).ToList();

        return new PurchasesReportDto(rows, rows.Sum(r => r.Total), rows.Count);
    }
}
