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

/// <summary>
/// Consultas del Inicio. Cada ida a la base cuesta (más aún contra Neon por Hyperdrive), así que se agrupan:
/// un solo <c>GROUP BY</c> para órdenes y otro para presupuestos, los conteos junto con el listado, y los nombres
/// de clientes, vehículos y mecánicos se resuelven <b>una sola vez</b> para todos los bloques.
/// </summary>
internal sealed class EfDashboardRepository : IDashboardRepository
{
    private const int LiveOrdersLimit = 8;
    private const int HotQuotesLimit = 5;
    private const int TodayAppointmentsLimit = 8;
    private const int LowStockLimit = 5;
    private const int RevenueDays = 7;

    private readonly DbContextOptions<AppDbContext> _options;
    private readonly ITenantContext _tenantContext;
    private readonly IActorContext _actor;
    private readonly TimeProvider _time;

    public EfDashboardRepository(
        DbContextOptions<AppDbContext> options,
        ITenantContext tenantContext,
        IActorContext actor,
        TimeProvider time)
    {
        _options = options;
        _tenantContext = tenantContext;
        _actor = actor;
        _time = time;
    }

    /// <summary>
    /// Cada bloque del Inicio corre en su propio <see cref="AppDbContext"/> (y su propia conexión) para poder
    /// ejecutarse en paralelo: un contexto no admite dos consultas a la vez. Todos comparten el mismo taller
    /// (<see cref="ITenantContext"/>), así que el filtro por taller sigue aplicándose en cada uno.
    /// </summary>
    private async Task<T> InOwnContextAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        await using var db = new AppDbContext(_options, _tenantContext, _actor, _time);
        return await work(db);
    }

    private sealed record VehicleInfo(string Plate, string? Label);

    private sealed record CustomerInfo(string Name, string? Phone);

    private sealed record LiveRow(
        Guid Id, int Number, WorkOrderStatus Status, WorkOrderPriority Priority,
        Guid CustomerId, Guid VehicleId, Guid? MechanicId, DateTimeOffset Since, int Days, bool Stuck);

    private sealed record HotRow(Guid Id, int Number, Guid CustomerId, Guid VehicleId, decimal Total, DateTimeOffset SentAt);

    private sealed record AppointmentRow(
        Guid Id, DateTimeOffset ScheduledAt, Guid CustomerId, Guid VehicleId, Guid? MechanicId, AppointmentStatus Status);

    public async Task<DashboardDto> GetSummaryAsync(DashboardScope scope, CancellationToken ct)
    {
        var now = _time.GetUtcNow();

        // Lo unico que depende de algo previo: la zona horaria y los umbrales del taller.
        var (zone, thresholds) = await InOwnContextAsync(db => GetSettingsAsync(db, ct));

        // Todos los bloques en paralelo, cada uno con su conexion.
        // Ordenes: un solo GROUP BY da el saldo, las abiertas y el conteo por estado.
        var orderGroupsTask = InOwnContextAsync(db => db.Set<WorkOrder>()
            .AsNoTracking()
            .GroupBy(w => w.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Balance = g.Sum(w => EF.Property<decimal>(w, "Balance")) })
            .ToListAsync(ct));

        // Presupuestos: un solo GROUP BY (borrador y enviados).
        var quoteGroupsTask = InOwnContextAsync(db => db.Set<Quote>()
            .AsNoTracking()
            .Where(q => q.Status == QuoteStatus.Draft || q.Status == QuoteStatus.Sent)
            .GroupBy(q => q.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Total = g.Sum(q => q.Total) })
            .ToListAsync(ct));

        var revenueTask = InOwnContextAsync(db => GetRevenueAsync(db, now, zone, ct));
        var agedTask = InOwnContextAsync(db => GetAgedReceivableAsync(db, now, thresholds.AgedReceivableDays, ct));
        var liveTask = InOwnContextAsync(db => GetLiveRowsAsync(db, now, thresholds.StuckOrderDays, ct));

        var hotTask = scope.Quotes
            ? InOwnContextAsync(db => GetHotRowsAsync(db, now, thresholds.HotQuoteHours, ct))
            : Task.FromResult<(int Total, List<HotRow> Rows)>((0, []));

        var appointmentsTask = scope.Appointments
            ? InOwnContextAsync(db => GetTodayRowsAsync(db, now, zone, ct))
            : Task.FromResult<(int Total, int Unassigned, List<AppointmentRow> Rows)>((0, 0, []));

        var cashTask = scope.Cash
            ? InOwnContextAsync(db => GetCashRowsAsync(db, ct))
            : Task.FromResult<((decimal Balance, Guid Id, string Name, CashAccountKind Kind)[] Accounts,
                List<(Guid AccountId, DateTimeOffset OpenedAt, Guid OpenedBy)> Sessions)>(([], []));

        // Con permiso de catalogo, el conteo sale de la misma consulta que el listado.
        var lowStockTask = scope.Stock
            ? InOwnContextAsync(db => GetLowStockAsync(db, ct))
            : InOwnContextAsync(async db => (await db.Set<CatalogItem>().AsNoTracking().Where(LowStockPredicate).CountAsync(ct),
                (IReadOnlyList<LowStockItemDto>)[]));

        await Task.WhenAll(
            orderGroupsTask, quoteGroupsTask, revenueTask, agedTask, liveTask, hotTask, appointmentsTask, cashTask, lowStockTask);

        var orderGroups = orderGroupsTask.Result;
        var quoteGroups = quoteGroupsTask.Result;
        var (revenueToday, paymentsToday, revenueDays) = revenueTask.Result;
        var aged = agedTask.Result;
        var live = liveTask.Result;
        var hot = hotTask.Result;
        var appointments = appointmentsTask.Result;
        var (cashAccounts, cashSessions) = cashTask.Result;
        var (lowStockItems, lowStockList) = lowStockTask.Result;
        IReadOnlyList<LowStockItemDto>? lowStock = scope.Stock ? lowStockList : null;

        int CountOf(WorkOrderStatus s) => orderGroups.FirstOrDefault(g => g.Status == s)?.Count ?? 0;

        // La columna generada `balance` ya retorna 0 para las OTs canceladas.
        var totalReceivable = orderGroups.Sum(g => g.Balance);
        var openWorkOrders = CountOf(WorkOrderStatus.Pending) + CountOf(WorkOrderStatus.InProgress) + CountOf(WorkOrderStatus.WaitingParts);

        var sent = quoteGroups.FirstOrDefault(g => g.Status == QuoteStatus.Sent);
        var pendingQuotes = quoteGroups.Sum(g => g.Count);

        // Nombres: tres consultas en paralelo para todos los bloques.
        var customerIds = live.Rows.Select(r => r.CustomerId)
            .Concat(hot.Rows.Select(r => r.CustomerId))
            .Concat(appointments.Rows.Select(r => r.CustomerId)).ToList();
        var vehicleIds = live.Rows.Select(r => r.VehicleId)
            .Concat(hot.Rows.Select(r => r.VehicleId))
            .Concat(appointments.Rows.Select(r => r.VehicleId)).ToList();
        var membershipIds = live.Rows.Where(r => r.MechanicId.HasValue).Select(r => r.MechanicId!.Value)
            .Concat(appointments.Rows.Where(r => r.MechanicId.HasValue).Select(r => r.MechanicId!.Value))
            .Concat(cashSessions.Select(x => x.OpenedBy)).ToList();

        var customersTask = InOwnContextAsync(db => CustomersAsync(db, customerIds, ct));
        var vehiclesTask = InOwnContextAsync(db => VehiclesAsync(db, vehicleIds, ct));
        var peopleTask = InOwnContextAsync(db => MembershipNamesAsync(db, membershipIds, ct));
        await Task.WhenAll(customersTask, vehiclesTask, peopleTask);

        var customers = customersTask.Result;
        var vehicles = vehiclesTask.Result;
        var people = peopleTask.Result;

        string CustomerName(Guid id) => customers.GetValueOrDefault(id)?.Name ?? string.Empty;
        string Plate(Guid id) => vehicles.GetValueOrDefault(id)?.Plate ?? string.Empty;
        string? PersonName(Guid? id) => id.HasValue ? people.GetValueOrDefault(id.Value) : null;

        var byStatus = new WorkOrdersByStatusDto(
            CountOf(WorkOrderStatus.Pending),
            CountOf(WorkOrderStatus.InProgress),
            CountOf(WorkOrderStatus.WaitingParts),
            CountOf(WorkOrderStatus.Completed),
            live.Stuck);

        var liveOrders = live.Rows.Select(r => new LiveOrderDto(
            r.Id, r.Number, r.Status, r.Priority,
            CustomerName(r.CustomerId), Plate(r.VehicleId), vehicles.GetValueOrDefault(r.VehicleId)?.Label,
            PersonName(r.MechanicId), r.Since, r.Days, r.Stuck)).ToList();

        HotQuotesDto? hotQuotes = scope.Quotes
            ? new HotQuotesDto(
                hot.Total,
                hot.Rows.Select(r => new HotQuoteDto(
                    r.Id, r.Number, CustomerName(r.CustomerId), customers.GetValueOrDefault(r.CustomerId)?.Phone,
                    Plate(r.VehicleId), r.Total, r.SentAt, Math.Max(0, (int)Math.Floor((now - r.SentAt).TotalHours)))).ToList())
            : null;

        TodayAppointmentsDto? todayAppointments = scope.Appointments
            ? new TodayAppointmentsDto(
                appointments.Total,
                appointments.Unassigned,
                appointments.Rows.Select(r => new TodayAppointmentDto(
                    r.Id, r.ScheduledAt, CustomerName(r.CustomerId), r.VehicleId, Plate(r.VehicleId), PersonName(r.MechanicId), r.Status)).ToList())
            : null;

        CashStatusDto? cash = null;
        if (scope.Cash)
        {
            var items = cashAccounts.Select(a =>
            {
                var session = cashSessions.FirstOrDefault(x => x.AccountId == a.Id);
                return new CashAccountStatusDto(
                    a.Id, a.Name, a.Kind, a.Balance,
                    session == default ? null : session.OpenedAt,
                    session == default ? null : people.GetValueOrDefault(session.OpenedBy));
            }).ToList();
            cash = new CashStatusDto(items.Sum(i => i.Balance), items);
        }

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
            liveOrders,
            sent?.Count ?? 0,
            sent?.Total ?? 0m,
            hotQuotes,
            todayAppointments,
            cash,
            lowStock);
    }

    // ── Zona horaria y umbrales del taller: una sola consulta ───────────────────

    private async Task<(TimeZoneInfo Zone, DashboardThresholdsDto Thresholds)> GetSettingsAsync(AppDbContext db, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId;
        var row = await (
                from t in db.Set<Tenant>().AsNoTracking()
                where t.Id == tenantId
                join s in db.Set<TenantSettings>().AsNoTracking() on t.Id equals s.TenantId
                select new { t.TimeZone, s.HotQuoteHours, s.StuckOrderDays, s.AgedReceivableDays })
            .FirstOrDefaultAsync(ct);

        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(row?.TimeZone ?? Tenant.DefaultTimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            zone = TimeZoneInfo.Utc;
        }

        var thresholds = row is null
            ? new DashboardThresholdsDto(
                TenantSettings.DefaultHotQuoteHours,
                TenantSettings.DefaultStuckOrderDays,
                TenantSettings.DefaultAgedReceivableDays)
            : new DashboardThresholdsDto(row.HotQuoteHours, row.StuckOrderDays, row.AgedReceivableDays);

        return (zone, thresholds);
    }

    private static DateTimeOffset StartOfLocalDayUtc(DateOnly day, TimeZoneInfo zone) =>
        new(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), zone), TimeSpan.Zero);

    private static DateOnly LocalDay(DateTimeOffset at, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);

    // ── Ingresos (cobros válidos + ventas de mostrador confirmadas), por día local ──

    private async Task<(decimal Today, int Count, IReadOnlyList<RevenueDayDto> Days)> GetRevenueAsync(AppDbContext db,
        DateTimeOffset now, TimeZoneInfo zone, CancellationToken ct)
    {
        var today = LocalDay(now, zone);
        var first = today.AddDays(-(RevenueDays - 1));
        var fromUtc = StartOfLocalDayUtc(first, zone);
        var toUtc = StartOfLocalDayUtc(today.AddDays(1), zone);

        var payments = await db.Set<Payment>()
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Valid && p.PaidAt >= fromUtc && p.PaidAt < toUtc)
            .Select(p => new { p.PaidAt, p.Amount })
            .ToListAsync(ct);

        var sales = await db.Set<CounterSale>()
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

    private async Task<AgedReceivableDto> GetAgedReceivableAsync(AppDbContext db, DateTimeOffset now, int days, CancellationToken ct)
    {
        var cutoff = now.AddDays(-days);

        var row = await db.Set<WorkOrder>()
            .AsNoTracking()
            .Where(w => w.Status != WorkOrderStatus.Cancelled
                     && EF.Property<decimal>(w, "Balance") > 0
                     && (w.DeliveredAt ?? w.CreatedAt) <= cutoff)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Amount = g.Sum(w => EF.Property<decimal>(w, "Balance")) })
            .FirstOrDefaultAsync(ct);

        return new AgedReceivableDto(row?.Count ?? 0, row?.Amount ?? 0m);
    }

    // ── Órdenes activas para «Taller en vivo»: «desde cuándo» = último cambio de estado ──

    private async Task<(List<LiveRow> Rows, int Stuck)> GetLiveRowsAsync(AppDbContext db,
        DateTimeOffset now, int stuckDays, CancellationToken ct)
    {
        var active = await db.Set<WorkOrder>()
            .AsNoTracking()
            .Where(w => w.Status == WorkOrderStatus.Pending
                     || w.Status == WorkOrderStatus.InProgress
                     || w.Status == WorkOrderStatus.WaitingParts
                     || w.Status == WorkOrderStatus.Completed)
            .Select(w => new { w.Id, w.Number, w.Status, w.Priority, w.CustomerId, w.VehicleId, w.AssignedMechanicId, w.CreatedAt })
            .ToListAsync(ct);

        if (active.Count == 0)
        {
            return ([], 0);
        }

        var ids = active.Select(a => a.Id).ToList();
        var lastChange = await db.Set<WorkOrderStatusChange>()
            .AsNoTracking()
            .Where(c => ids.Contains(c.WorkOrderId))
            .GroupBy(c => c.WorkOrderId)
            .Select(g => new { Id = g.Key, At = g.Max(c => c.ChangedAt) })
            .ToDictionaryAsync(x => x.Id, x => x.At, ct);

        var rows = active.Select(a =>
        {
            var since = lastChange.TryGetValue(a.Id, out var at) ? at : a.CreatedAt;
            var days = Math.Max(0, (int)Math.Floor((now - since).TotalDays));
            return new LiveRow(a.Id, a.Number, a.Status, a.Priority, a.CustomerId, a.VehicleId, a.AssignedMechanicId, since, days, days >= stuckDays);
        }).ToList();

        // Primero las trabadas y, dentro de cada grupo, las que más tiempo llevan en el estado.
        var top = rows.OrderByDescending(r => r.Stuck).ThenBy(r => r.Since).Take(LiveOrdersLimit).ToList();
        return (top, rows.Count(r => r.Stuck));
    }

    // ── Presupuestos «calientes»: el listado y el total en la misma consulta ────

    private async Task<(int Total, List<HotRow> Rows)> GetHotRowsAsync(AppDbContext db, DateTimeOffset now, int hours, CancellationToken ct)
    {
        var cutoff = now.AddHours(-hours);

        var query = db.Set<Quote>()
            .AsNoTracking()
            .Where(q => q.Status == QuoteStatus.Sent && q.SentAt != null && q.SentAt <= cutoff);

        var raw = await query
            .OrderBy(q => q.SentAt)
            .Take(HotQuotesLimit)
            .Select(q => new { q.Id, q.Number, q.CustomerId, q.VehicleId, q.Total, SentAt = q.SentAt!.Value, All = query.Count() })
            .ToListAsync(ct);

        return (raw.Count == 0 ? 0 : raw[0].All,
            raw.Select(r => new HotRow(r.Id, r.Number, r.CustomerId, r.VehicleId, r.Total, r.SentAt)).ToList());
    }

    // ── Turnos de hoy (día local del taller), pendientes o confirmados ──────────

    private async Task<(int Total, int Unassigned, List<AppointmentRow> Rows)> GetTodayRowsAsync(AppDbContext db,
        DateTimeOffset now, TimeZoneInfo zone, CancellationToken ct)
    {
        var today = LocalDay(now, zone);
        var fromUtc = StartOfLocalDayUtc(today, zone);
        var toUtc = StartOfLocalDayUtc(today.AddDays(1), zone);

        var query = db.Set<Appointment>()
            .AsNoTracking()
            .Where(a => a.ScheduledAt >= fromUtc && a.ScheduledAt < toUtc
                     && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed));

        var counts = await query
            .GroupBy(_ => 1)
            .Select(g => new { Total = g.Count(), Unassigned = g.Count(a => a.MechanicMembershipId == null) })
            .FirstOrDefaultAsync(ct);

        if (counts is null)
        {
            return (0, 0, []);
        }

        var rows = await query
            .OrderBy(a => a.ScheduledAt)
            .Take(TodayAppointmentsLimit)
            .Select(a => new AppointmentRow(a.Id, a.ScheduledAt, a.CustomerId, a.VehicleId, a.MechanicMembershipId, a.Status))
            .ToListAsync(ct);

        return (counts.Total, counts.Unassigned, rows);
    }

    // ── Caja: saldo por cuenta activa (en la misma consulta) y sesiones abiertas ──

    private async Task<((decimal Balance, Guid Id, string Name, CashAccountKind Kind)[] Accounts,
        List<(Guid AccountId, DateTimeOffset OpenedAt, Guid OpenedBy)> Sessions)> GetCashRowsAsync(AppDbContext db, CancellationToken ct)
    {
        var accounts = await db.Set<CashAccount>()
            .AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.Name)
            .Select(a => new
            {
                a.Id,
                a.Name,
                a.Kind,
                Balance = db.Set<CashMovement>()
                    .Where(m => m.AccountId == a.Id)
                    .Sum(m => m.Direction == CashMovementDirection.In ? m.Amount : -m.Amount),
            })
            .ToListAsync(ct);

        if (accounts.Count == 0)
        {
            return ([], []);
        }

        var sessions = await db.Set<CashSession>()
            .AsNoTracking()
            .Where(s => s.Status == CashSessionStatus.Open)
            .Select(s => new { s.AccountId, s.OpenedAt, s.OpenedByMembershipId })
            .ToListAsync(ct);

        return (
            accounts.Select(a => (a.Balance, a.Id, a.Name, a.Kind)).ToArray(),
            sessions.Select(s => (s.AccountId, s.OpenedAt, s.OpenedByMembershipId)).ToList());
    }

    // ── Stock bajo: los más urgentes primero (el más negativo respecto de su mínimo) ──

    private static readonly System.Linq.Expressions.Expression<Func<CatalogItem, bool>> LowStockPredicate =
        c => c.TrackStock && (c.StockQty < 0 || (c.StockMin.HasValue && c.StockQty < c.StockMin.Value));

    private async Task<(int Total, IReadOnlyList<LowStockItemDto> Items)> GetLowStockAsync(AppDbContext db, CancellationToken ct)
    {
        var query = db.Set<CatalogItem>().AsNoTracking().Where(LowStockPredicate);

        var raw = await query
            .OrderBy(c => c.StockQty - (c.StockMin ?? 0m))
            .ThenBy(c => c.Name)
            .Take(LowStockLimit)
            .Select(c => new { c.Id, c.Name, c.Sku, c.StockQty, c.StockMin, All = query.Count() })
            .ToListAsync(ct);

        return (raw.Count == 0 ? 0 : raw[0].All,
            raw.Select(c => new LowStockItemDto(c.Id, c.Name, c.Sku, c.StockQty, c.StockMin)).ToList());
    }

    // ── Resolución de nombres (los clientes y vehículos dados de baja siguen figurando) ──

    private async Task<Dictionary<Guid, CustomerInfo>> CustomersAsync(AppDbContext db, IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0)
        {
            return [];
        }

        return await db.Set<Customer>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => list.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, c.Phone })
            .ToDictionaryAsync(c => c.Id, c => new CustomerInfo(c.Name, c.Phone), ct);
    }

    private async Task<Dictionary<Guid, VehicleInfo>> VehiclesAsync(AppDbContext db, IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0)
        {
            return [];
        }

        var raw = await db.Set<Vehicle>()
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
    private async Task<Dictionary<Guid, string>> MembershipNamesAsync(AppDbContext db, IEnumerable<Guid> membershipIds, CancellationToken ct)
    {
        var list = membershipIds.Distinct().ToList();
        if (list.Count == 0)
        {
            return [];
        }

        return await db.Set<Membership>()
            .AsNoTracking()
            .Where(m => list.Contains(m.Id))
            .Join(db.Set<User>().AsNoTracking(), m => m.UserId, u => u.Id, (m, u) => new { m.Id, u.DisplayName })
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
    }
}
