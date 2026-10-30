using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Common;
using TallerControl.Application.Dashboard;
using TallerControl.Domain.Cash;
using TallerControl.Domain.Catalog;
using TallerControl.Domain.CounterSales;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Payments;
using TallerControl.Domain.Quotes;
using TallerControl.Domain.Scheduling;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Users;
using TallerControl.Domain.Vehicles;
using TallerControl.Domain.Work;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Dashboard;

internal sealed class EfDashboardRepository : IDashboardRepository
{
    private const int LiveOrdersLimit = 8;
    private const int HotQuotesLimit = 5;
    private const int TodayAppointmentsLimit = 8;
    private const int LowStockLimit = 5;
    private const int RevenueDays = 7;

    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _time;

    public EfDashboardRepository(AppDbContext db, ITenantContext tenantContext, TimeProvider time)
    {
        _db = db;
        _tenantContext = tenantContext;
        _time = time;
    }

    public async Task<DashboardDto> GetSummaryAsync(DashboardScope scope, CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        var zone = await GetTenantZoneAsync(ct);
        var thresholds = await GetThresholdsAsync(ct);

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
            .Where(LowStockPredicate)
            .CountAsync(ct);

        var (revenueToday, paymentsToday, revenueDays) = await GetRevenueAsync(now, zone, ct);
        var aged = await GetAgedReceivableAsync(now, thresholds.AgedReceivableDays, ct);
        var (byStatus, live) = await GetOrdersAsync(now, thresholds.StuckOrderDays, ct);

        var sentQuotes = await _db.Set<Quote>()
            .AsNoTracking()
            .Where(q => q.Status == QuoteStatus.Sent)
            .CountAsync(ct);
        var sentQuotesTotal = await _db.Set<Quote>()
            .AsNoTracking()
            .Where(q => q.Status == QuoteStatus.Sent)
            .SumAsync(q => q.Total, ct);

        var hotQuotes = scope.Quotes ? await GetHotQuotesAsync(now, thresholds.HotQuoteHours, ct) : null;
        var appointments = scope.Appointments ? await GetTodayAppointmentsAsync(now, zone, ct) : null;
        var cash = scope.Cash ? await GetCashAsync(ct) : null;
        var lowStock = scope.Stock ? await GetLowStockAsync(ct) : null;

        return new DashboardDto(
            totalReceivable,
            openWorkOrders,
            pendingQuotes,
            lowStockItems,
            thresholds,
            revenueToday,
            paymentsToday,
            revenueDays,
            aged,
            byStatus,
            live,
            sentQuotes,
            sentQuotesTotal,
            hotQuotes,
            appointments,
            cash,
            lowStock);
    }

    // ── Zona horaria y umbrales del taller ──────────────────────────────────────

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

    private async Task<DashboardThresholdsDto> GetThresholdsAsync(CancellationToken ct)
    {
        var settings = await _db.Set<TenantSettings>()
            .AsNoTracking()
            .Select(s => new { s.HotQuoteHours, s.StuckOrderDays, s.AgedReceivableDays })
            .FirstOrDefaultAsync(ct);

        return settings is null
            ? new DashboardThresholdsDto(
                TenantSettings.DefaultHotQuoteHours,
                TenantSettings.DefaultStuckOrderDays,
                TenantSettings.DefaultAgedReceivableDays)
            : new DashboardThresholdsDto(settings.HotQuoteHours, settings.StuckOrderDays, settings.AgedReceivableDays);
    }

    private static DateTimeOffset StartOfLocalDayUtc(DateOnly day, TimeZoneInfo zone) =>
        new(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), zone), TimeSpan.Zero);

    private static DateOnly LocalDay(DateTimeOffset at, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);

    // ── Ingresos (cobros válidos + ventas de mostrador confirmadas), por día local ──

    private async Task<(decimal Today, int Count, IReadOnlyList<RevenueDayDto> Days)> GetRevenueAsync(
        DateTimeOffset now, TimeZoneInfo zone, CancellationToken ct)
    {
        var today = LocalDay(now, zone);
        var first = today.AddDays(-(RevenueDays - 1));
        var fromUtc = StartOfLocalDayUtc(first, zone);
        var toUtc = StartOfLocalDayUtc(today.AddDays(1), zone);

        var payments = await _db.Set<Payment>()
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Valid && p.PaidAt >= fromUtc && p.PaidAt < toUtc)
            .Select(p => new { p.PaidAt, p.Amount })
            .ToListAsync(ct);

        var sales = await _db.Set<CounterSale>()
            .AsNoTracking()
            .Where(s => s.Status == CounterSaleStatus.Confirmed && s.Date >= fromUtc && s.Date < toUtc)
            .Select(s => new { s.Date, s.Total })
            .ToListAsync(ct);

        var entries = payments.Select(p => (Day: LocalDay(p.PaidAt, zone), p.Amount))
            .Concat(sales.Select(s => (Day: LocalDay(s.Date, zone), Amount: s.Total)))
            .ToList();

        var days = Enumerable.Range(0, RevenueDays)
            .Select(i => first.AddDays(i))
            .Select(d => new RevenueDayDto(d, entries.Where(e => e.Day == d).Sum(e => e.Amount)))
            .ToList();

        var todayEntries = entries.Where(e => e.Day == today).ToList();
        return (todayEntries.Sum(e => e.Amount), todayEntries.Count, days);
    }

    // ── Saldo antiguo (D-39): desde la entrega, o desde la creación si no se entregó ──

    private async Task<AgedReceivableDto> GetAgedReceivableAsync(DateTimeOffset now, int days, CancellationToken ct)
    {
        var cutoff = now.AddDays(-days);

        var query = _db.Set<WorkOrder>()
            .AsNoTracking()
            .Where(w => w.Status != WorkOrderStatus.Cancelled
                     && EF.Property<decimal>(w, "Balance") > 0
                     && (w.DeliveredAt ?? w.CreatedAt) <= cutoff);

        var count = await query.CountAsync(ct);
        var amount = count == 0 ? 0m : await query.SumAsync(w => EF.Property<decimal>(w, "Balance"), ct);
        return new AgedReceivableDto(count, amount);
    }

    // ── Órdenes activas: conteo por estado y «Taller en vivo» ───────────────────

    private async Task<(WorkOrdersByStatusDto ByStatus, IReadOnlyList<LiveOrderDto> Live)> GetOrdersAsync(
        DateTimeOffset now, int stuckDays, CancellationToken ct)
    {
        var active = await _db.Set<WorkOrder>()
            .AsNoTracking()
            .Where(w => w.Status == WorkOrderStatus.Pending
                     || w.Status == WorkOrderStatus.InProgress
                     || w.Status == WorkOrderStatus.WaitingParts
                     || w.Status == WorkOrderStatus.Completed)
            .Select(w => new
            {
                w.Id,
                w.Number,
                w.Status,
                w.Priority,
                w.CustomerId,
                w.VehicleId,
                w.AssignedMechanicId,
                w.CreatedAt,
            })
            .ToListAsync(ct);

        if (active.Count == 0)
        {
            return (new WorkOrdersByStatusDto(0, 0, 0, 0, 0), []);
        }

        // «Desde cuándo está en este estado»: el último cambio de estado, o la creación si nunca cambió.
        var ids = active.Select(a => a.Id).ToList();
        var lastChange = await _db.Set<WorkOrderStatusChange>()
            .AsNoTracking()
            .Where(c => ids.Contains(c.WorkOrderId))
            .GroupBy(c => c.WorkOrderId)
            .Select(g => new { Id = g.Key, At = g.Max(c => c.ChangedAt) })
            .ToDictionaryAsync(x => x.Id, x => x.At, ct);

        var rows = active.Select(a =>
        {
            var since = lastChange.TryGetValue(a.Id, out var at) ? at : a.CreatedAt;
            var daysInStatus = Math.Max(0, (int)Math.Floor((now - since).TotalDays));
            return new { Order = a, Since = since, Days = daysInStatus, Stuck = daysInStatus >= stuckDays };
        }).ToList();

        var byStatus = new WorkOrdersByStatusDto(
            rows.Count(r => r.Order.Status == WorkOrderStatus.Pending),
            rows.Count(r => r.Order.Status == WorkOrderStatus.InProgress),
            rows.Count(r => r.Order.Status == WorkOrderStatus.WaitingParts),
            rows.Count(r => r.Order.Status == WorkOrderStatus.Completed),
            rows.Count(r => r.Stuck));

        // Primero las trabadas y, dentro de cada grupo, las que más tiempo llevan en el estado.
        var top = rows
            .OrderByDescending(r => r.Stuck)
            .ThenBy(r => r.Since)
            .Take(LiveOrdersLimit)
            .ToList();

        var customers = await CustomerNamesAsync(top.Select(r => r.Order.CustomerId), ct);
        var vehicles = await VehiclesAsync(top.Select(r => r.Order.VehicleId), ct);
        var mechanics = await MechanicNamesAsync(
            top.Where(r => r.Order.AssignedMechanicId.HasValue).Select(r => r.Order.AssignedMechanicId!.Value), ct);

        var live = top.Select(r =>
        {
            var vehicle = vehicles.GetValueOrDefault(r.Order.VehicleId);
            return new LiveOrderDto(
                r.Order.Id,
                r.Order.Number,
                r.Order.Status,
                r.Order.Priority,
                customers.GetValueOrDefault(r.Order.CustomerId, string.Empty),
                vehicle?.Plate ?? string.Empty,
                vehicle?.Label,
                r.Order.AssignedMechanicId.HasValue ? mechanics.GetValueOrDefault(r.Order.AssignedMechanicId.Value) : null,
                r.Since,
                r.Days,
                r.Stuck);
        }).ToList();

        return (byStatus, live);
    }

    // ── Presupuestos «calientes»: enviados hace más de N horas sin respuesta ────

    private async Task<HotQuotesDto> GetHotQuotesAsync(DateTimeOffset now, int hours, CancellationToken ct)
    {
        var cutoff = now.AddHours(-hours);

        var query = _db.Set<Quote>()
            .AsNoTracking()
            .Where(q => q.Status == QuoteStatus.Sent && q.SentAt != null && q.SentAt <= cutoff);

        var total = await query.CountAsync(ct);
        if (total == 0)
        {
            return new HotQuotesDto(0, []);
        }

        var raw = await query
            .OrderBy(q => q.SentAt)
            .Take(HotQuotesLimit)
            .Select(q => new { q.Id, q.Number, q.CustomerId, q.VehicleId, q.Total, SentAt = q.SentAt!.Value })
            .ToListAsync(ct);

        var customerIds = raw.Select(r => r.CustomerId).Distinct().ToList();
        var customers = await _db.Set<Customer>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, c.Phone })
            .ToDictionaryAsync(c => c.Id, ct);
        var vehicles = await VehiclesAsync(raw.Select(r => r.VehicleId), ct);

        var items = raw.Select(r => new HotQuoteDto(
            r.Id,
            r.Number,
            customers.TryGetValue(r.CustomerId, out var c) ? c.Name : string.Empty,
            c?.Phone,
            vehicles.GetValueOrDefault(r.VehicleId)?.Plate ?? string.Empty,
            r.Total,
            r.SentAt,
            Math.Max(0, (int)Math.Floor((now - r.SentAt).TotalHours)))).ToList();

        return new HotQuotesDto(total, items);
    }

    // ── Turnos de hoy (día local del taller), pendientes o confirmados ──────────

    private async Task<TodayAppointmentsDto> GetTodayAppointmentsAsync(DateTimeOffset now, TimeZoneInfo zone, CancellationToken ct)
    {
        var today = LocalDay(now, zone);
        var fromUtc = StartOfLocalDayUtc(today, zone);
        var toUtc = StartOfLocalDayUtc(today.AddDays(1), zone);

        var query = _db.Set<Appointment>()
            .AsNoTracking()
            .Where(a => a.ScheduledAt >= fromUtc && a.ScheduledAt < toUtc
                     && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed));

        var total = await query.CountAsync(ct);
        if (total == 0)
        {
            return new TodayAppointmentsDto(0, 0, []);
        }

        var unassigned = await query.CountAsync(a => a.MechanicMembershipId == null, ct);

        var raw = await query
            .OrderBy(a => a.ScheduledAt)
            .Take(TodayAppointmentsLimit)
            .Select(a => new { a.Id, a.ScheduledAt, a.CustomerId, a.VehicleId, a.MechanicMembershipId, a.Status })
            .ToListAsync(ct);

        var customers = await CustomerNamesAsync(raw.Select(r => r.CustomerId), ct);
        var vehicles = await VehiclesAsync(raw.Select(r => r.VehicleId), ct);
        var mechanics = await MechanicNamesAsync(
            raw.Where(r => r.MechanicMembershipId.HasValue).Select(r => r.MechanicMembershipId!.Value), ct);

        var items = raw.Select(r => new TodayAppointmentDto(
            r.Id,
            r.ScheduledAt,
            customers.GetValueOrDefault(r.CustomerId, string.Empty),
            r.VehicleId,
            vehicles.GetValueOrDefault(r.VehicleId)?.Plate ?? string.Empty,
            r.MechanicMembershipId.HasValue ? mechanics.GetValueOrDefault(r.MechanicMembershipId.Value) : null,
            r.Status)).ToList();

        return new TodayAppointmentsDto(total, unassigned, items);
    }

    // ── Caja: saldo por cuenta activa y quién tiene la sesión abierta ───────────

    private async Task<CashStatusDto> GetCashAsync(CancellationToken ct)
    {
        var accounts = await _db.Set<CashAccount>()
            .AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.Name)
            .Select(a => new { a.Id, a.Name, a.Kind })
            .ToListAsync(ct);

        if (accounts.Count == 0)
        {
            return new CashStatusDto(0m, []);
        }

        var ids = accounts.Select(a => a.Id).ToList();

        var balances = await _db.Set<CashMovement>()
            .AsNoTracking()
            .Where(m => ids.Contains(m.AccountId))
            .GroupBy(m => m.AccountId)
            .Select(g => new
            {
                Id = g.Key,
                Balance = g.Sum(m => m.Direction == CashMovementDirection.In ? m.Amount : -m.Amount),
            })
            .ToDictionaryAsync(x => x.Id, x => x.Balance, ct);

        var sessions = await _db.Set<CashSession>()
            .AsNoTracking()
            .Where(s => s.Status == CashSessionStatus.Open && ids.Contains(s.AccountId))
            .Select(s => new { s.AccountId, s.OpenedAt, s.OpenedByMembershipId })
            .ToListAsync(ct);

        var openers = await MechanicNamesAsync(sessions.Select(s => s.OpenedByMembershipId), ct);

        var items = accounts.Select(a =>
        {
            var session = sessions.FirstOrDefault(s => s.AccountId == a.Id);
            return new CashAccountStatusDto(
                a.Id,
                a.Name,
                a.Kind,
                balances.GetValueOrDefault(a.Id),
                session?.OpenedAt,
                session is null ? null : openers.GetValueOrDefault(session.OpenedByMembershipId));
        }).ToList();

        return new CashStatusDto(items.Sum(i => i.Balance), items);
    }

    // ── Stock bajo: los más urgentes primero (el más negativo respecto de su mínimo) ──

    private static readonly System.Linq.Expressions.Expression<Func<CatalogItem, bool>> LowStockPredicate =
        c => c.TrackStock && (c.StockQty < 0 || (c.StockMin.HasValue && c.StockQty < c.StockMin.Value));

    private async Task<IReadOnlyList<LowStockItemDto>> GetLowStockAsync(CancellationToken ct) =>
        await _db.Set<CatalogItem>()
            .AsNoTracking()
            .Where(LowStockPredicate)
            .OrderBy(c => c.StockQty - (c.StockMin ?? 0m))
            .ThenBy(c => c.Name)
            .Take(LowStockLimit)
            .Select(c => new LowStockItemDto(c.Id, c.Name, c.Sku, c.StockQty, c.StockMin))
            .ToListAsync(ct);

    // ── Resolución de nombres (los clientes y vehículos dados de baja siguen figurando) ──

    private async Task<Dictionary<Guid, string>> CustomerNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await _db.Set<Customer>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => list.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);
    }

    private sealed record VehicleInfo(string Plate, string? Label);

    private async Task<Dictionary<Guid, VehicleInfo>> VehiclesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        var raw = await _db.Set<Vehicle>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(v => list.Contains(v.Id))
            .Select(v => new { v.Id, v.Plate, v.Brand, v.Model })
            .ToListAsync(ct);

        return raw.ToDictionary(
            v => v.Id,
            v => new VehicleInfo(v.Plate, string.Join(' ', new[] { v.Brand, v.Model }.Where(x => !string.IsNullOrWhiteSpace(x)))));
    }

    /// <summary>Nombre visible (del usuario) de cada membresía.</summary>
    private async Task<Dictionary<Guid, string>> MechanicNamesAsync(IEnumerable<Guid> membershipIds, CancellationToken ct)
    {
        var list = membershipIds.Distinct().ToList();
        if (list.Count == 0)
        {
            return [];
        }

        return await _db.Set<Membership>()
            .AsNoTracking()
            .Where(m => list.Contains(m.Id))
            .Join(_db.Set<User>().AsNoTracking(), m => m.UserId, u => u.Id, (m, u) => new { m.Id, u.DisplayName })
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
    }
}
