using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Common;
using TallerControl.Application.Reports;
using TallerControl.Domain.Catalog;
using TallerControl.Domain.CounterSales;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Payments;
using TallerControl.Domain.Purchases;
using TallerControl.Domain.Suppliers;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Vehicles;
using TallerControl.Domain.Work;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Reports;

internal sealed class EfReportsRepository : IReportsRepository
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public EfReportsRepository(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    private async Task<TimeZoneInfo> GetTenantZoneAsync(CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId;
        var name = await _db.Set<Tenant>()
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.TimeZone)
            .FirstOrDefaultAsync(ct);
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(name ?? Tenant.DefaultTimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>Rango [desde, hasta+1 día) en UTC para días calendario de la zona horaria del taller.</summary>
    private static (DateTimeOffset FromUtc, DateTimeOffset ToUtcExclusive) LocalRangeToUtc(DateOnly from, DateOnly to, TimeZoneInfo zone)
    {
        DateTimeOffset StartOf(DateOnly d) =>
            new(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(d.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), zone), TimeSpan.Zero);
        return (StartOf(from), StartOf(to.AddDays(1)));
    }

    public async Task<RevenueReportDto> GetRevenueAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        // Los días se arman en la zona horaria del taller: un cobro a las 23:30 (hora local) cuenta ese día,
        // no el siguiente en UTC.
        var zone = await GetTenantZoneAsync(ct);
        var (fromUtc, toUtc) = LocalRangeToUtc(from, to, zone);

        var payments = await _db.Set<Payment>()
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Valid && p.PaidAt >= fromUtc && p.PaidAt < toUtc)
            .Select(p => new { p.PaidAt, p.Amount, p.Method })
            .ToListAsync(ct);

        // Las ventas de mostrador confirmadas también son ingresos del taller.
        var sales = await _db.Set<CounterSale>()
            .AsNoTracking()
            .Where(s => s.Status == CounterSaleStatus.Confirmed && s.Date >= fromUtc && s.Date < toUtc)
            .Select(s => new { s.Date, s.Total })
            .ToListAsync(ct);

        DateOnly LocalDay(DateTimeOffset at) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);

        var entries = payments.Select(p => (Day: LocalDay(p.PaidAt), p.Amount))
            .Concat(sales.Select(s => (Day: LocalDay(s.Date), Amount: s.Total)))
            .ToList();

        var rows = entries
            .GroupBy(e => e.Day)
            .Select(g => new RevenueReportRow(g.Key, g.Sum(e => e.Amount), g.Count()))
            .OrderBy(r => r.Date)
            .ToList();

        var breakdown = payments
            .GroupBy(p => ToSnake(p.Method.ToString()))
            .Select(g => new RevenueBreakdownRow(g.Key, g.Sum(p => p.Amount), g.Count()))
            .Concat(sales.Count > 0
                ? [new RevenueBreakdownRow("counter_sale", sales.Sum(s => s.Total), sales.Count)]
                : [])
            .OrderByDescending(b => b.Amount)
            .ToList();

        return new RevenueReportDto(rows, rows.Sum(r => r.Amount), rows.Sum(r => r.PaymentCount), breakdown);
    }

    private static string ToSnake(string value) =>
        string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));

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
        // La fecha de factura es un día calendario guardado a medianoche UTC (no depende de la zona horaria).
        var fromUtc = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toUtc = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        // Solo compras confirmadas: los borradores todavía no son una compra (no movieron stock ni caja).
        var raw = await _db.Set<Purchase>()
            .AsNoTracking()
            .Where(p => p.InvoiceDate >= fromUtc && p.InvoiceDate < toUtc
                     && p.Status == PurchaseStatus.Confirmed)
            .Select(p => new
            {
                Id = p.Id.ToString(),
                Number = p.DocumentNumber,
                p.SupplierId,
                CreatedAt = p.InvoiceDate,
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
