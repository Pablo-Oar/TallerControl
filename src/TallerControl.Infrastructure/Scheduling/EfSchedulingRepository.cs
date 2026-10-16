using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Common;
using TallerControl.Application.Scheduling;
using TallerControl.Domain.Catalog;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Scheduling;
using TallerControl.Domain.Users;
using TallerControl.Domain.Vehicles;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Scheduling;

internal sealed class EfSchedulingRepository : ISchedulingRepository
{
    private readonly AppDbContext _db;

    public EfSchedulingRepository(AppDbContext db) => _db = db;

    public async Task<Page<AppointmentListDto>> SearchAsync(
        DateOnly? date, Guid? mechanicMembershipId, AppointmentStatus? status,
        int page, int pageSize, CancellationToken ct)
    {
        var query = _db.Set<Appointment>().AsNoTracking()
            .Join(_db.Set<Customer>().AsNoTracking(), a => a.CustomerId, c => c.Id,
                (a, c) => new { Appt = a, CustomerName = c.Name })
            .Join(_db.Set<Vehicle>().AsNoTracking(), x => x.Appt.VehicleId, v => v.Id,
                (x, v) => new { x.Appt, x.CustomerName, VehiclePlate = v.Plate });

        if (date.HasValue)
        {
            var start = date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end = date.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            var startDto = new DateTimeOffset(start, TimeSpan.Zero);
            var endDto = new DateTimeOffset(end, TimeSpan.Zero);
            query = query.Where(x => x.Appt.ScheduledAt >= startDto && x.Appt.ScheduledAt <= endDto);
        }

        if (mechanicMembershipId.HasValue)
            query = query.Where(x => x.Appt.MechanicMembershipId == mechanicMembershipId.Value);

        if (status.HasValue)
            query = query.Where(x => x.Appt.Status == status.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(x => x.Appt.ScheduledAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Appt,
                x.CustomerName,
                x.VehiclePlate,
                MechanicName = x.Appt.MechanicMembershipId == null
                    ? (string?)null
                    : _db.Set<Membership>()
                        .Where(m => m.Id == x.Appt.MechanicMembershipId)
                        .Join(_db.Set<User>(), m => m.UserId, u => u.Id, (m, u) => u.DisplayName)
                        .FirstOrDefault(),
            })
            .ToListAsync(ct);

        var dtos = items.Select(x => new AppointmentListDto(
            x.Appt.Id,
            x.Appt.CustomerId,
            x.CustomerName,
            x.Appt.VehicleId,
            x.VehiclePlate,
            x.Appt.MechanicMembershipId,
            x.MechanicName,
            x.Appt.ScheduledAt,
            x.Appt.TotalDurationMinutes,
            x.Appt.Status,
            x.Appt.Notes)).ToList();

        return new Page<AppointmentListDto>(dtos, page, pageSize, total);
    }

    public async Task<AppointmentDto?> GetFullAsync(Guid id, CancellationToken ct)
    {
        var appt = await _db.Set<Appointment>()
            .AsNoTracking()
            .Include(a => a.Services)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (appt is null) return null;

        var customer = await _db.Set<Customer>().AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == appt.CustomerId, ct);

        var vehicle = await _db.Set<Vehicle>().AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == appt.VehicleId, ct);

        string? mechanicName = null;
        if (appt.MechanicMembershipId.HasValue)
        {
            mechanicName = await _db.Set<Membership>()
                .Where(m => m.Id == appt.MechanicMembershipId.Value)
                .Join(_db.Set<User>(), m => m.UserId, u => u.Id, (m, u) => u.DisplayName)
                .FirstOrDefaultAsync(ct);
        }

        var serviceDtos = await Task.WhenAll(appt.Services.Select(async s =>
        {
            var item = await _db.Set<CatalogItem>().AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == s.CatalogItemId, ct);
            return new AppointmentServiceDto(
                s.CatalogItemId,
                item?.Name ?? string.Empty,
                s.DurationMinutes,
                s.BufferMinutes);
        }));

        return new AppointmentDto(
            appt.Id,
            appt.CustomerId,
            customer?.Name ?? string.Empty,
            appt.VehicleId,
            vehicle?.Plate ?? string.Empty,
            appt.MechanicMembershipId,
            mechanicName,
            appt.ScheduledAt,
            appt.TotalDurationMinutes,
            appt.Status,
            appt.Notes,
            appt.CancellationReason,
            serviceDtos,
            appt.Version);
    }

    public Task<Appointment?> FindByIdAsync(Guid id, CancellationToken ct) =>
        _db.Set<Appointment>()
            .Include(a => a.Services)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<Appointment?> FindByConfirmationTokenHashAsync(byte[] tokenHash, CancellationToken ct) =>
        _db.Set<Appointment>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.ConfirmationTokenHash != null
                && a.ConfirmationTokenHash == tokenHash, ct);

    public async Task AddAsync(Appointment appointment, CancellationToken ct) =>
        await _db.Set<Appointment>().AddAsync(appointment, ct);

    // ── Availability ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<AvailabilityRuleDto>> GetRulesAsync(
        Guid? mechanicMembershipId, CancellationToken ct)
    {
        var query = _db.Set<AvailabilityRule>().AsNoTracking();

        if (mechanicMembershipId.HasValue)
            query = query.Where(r => r.MechanicMembershipId == mechanicMembershipId.Value);

        var rules = await query.ToListAsync(ct);

        var dtos = new List<AvailabilityRuleDto>();
        foreach (var r in rules)
        {
            var name = await _db.Set<Membership>()
                .Where(m => m.Id == r.MechanicMembershipId)
                .Join(_db.Set<User>(), m => m.UserId, u => u.Id, (m, u) => u.DisplayName)
                .FirstOrDefaultAsync(ct) ?? string.Empty;

            dtos.Add(new AvailabilityRuleDto(r.Id, r.MechanicMembershipId, name,
                r.DayOfWeek, r.StartTime, r.EndTime));
        }

        return dtos;
    }

    public Task<AvailabilityRule?> FindRuleByIdAsync(Guid id, CancellationToken ct) =>
        _db.Set<AvailabilityRule>().FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task AddRuleAsync(AvailabilityRule rule, CancellationToken ct) =>
        await _db.Set<AvailabilityRule>().AddAsync(rule, ct);

    public async Task DeleteRuleAsync(Guid id, CancellationToken ct)
    {
        var rule = await _db.Set<AvailabilityRule>().FindAsync([id], ct);
        if (rule is not null) _db.Set<AvailabilityRule>().Remove(rule);
    }

    public async Task<IReadOnlyList<AvailabilityExceptionDto>> GetExceptionsAsync(
        DateOnly date, CancellationToken ct)
    {
        var exceptions = await _db.Set<AvailabilityException>().AsNoTracking()
            .Where(e => e.Date == date)
            .ToListAsync(ct);

        var dtos = new List<AvailabilityExceptionDto>();
        foreach (var e in exceptions)
        {
            string? name = null;
            if (e.MechanicMembershipId.HasValue)
            {
                name = await _db.Set<Membership>()
                    .Where(m => m.Id == e.MechanicMembershipId.Value)
                    .Join(_db.Set<User>(), m => m.UserId, u => u.Id, (m, u) => u.DisplayName)
                    .FirstOrDefaultAsync(ct);
            }

            dtos.Add(new AvailabilityExceptionDto(
                e.Id, e.MechanicMembershipId, name,
                e.Date, e.IsBlocked, e.StartTime, e.EndTime, e.Reason));
        }

        return dtos;
    }

    public Task<AvailabilityException?> FindExceptionByIdAsync(Guid id, CancellationToken ct) =>
        _db.Set<AvailabilityException>().FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task AddExceptionAsync(AvailabilityException exception, CancellationToken ct) =>
        await _db.Set<AvailabilityException>().AddAsync(exception, ct);

    public void RemoveException(AvailabilityException exception) =>
        _db.Set<AvailabilityException>().Remove(exception);

    public async Task<IReadOnlyList<AvailabilitySlotDto>> GetAvailabilitySlotsAsync(
        DateOnly date, int requiredMinutes, CancellationToken ct)
    {
        var dayOfWeek = date.DayOfWeek;

        // Reglas activas para ese día de la semana.
        var rules = await _db.Set<AvailabilityRule>().AsNoTracking()
            .Where(r => r.DayOfWeek == dayOfWeek)
            .ToListAsync(ct);

        // Excepciones del día (bloqueos o cambios de horario).
        var exceptions = await _db.Set<AvailabilityException>().AsNoTracking()
            .Where(e => e.Date == date)
            .ToListAsync(ct);

        // Turnos ya agendados ese día.
        var dayStart = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), TimeSpan.Zero);
        var dayEnd = new DateTimeOffset(date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc), TimeSpan.Zero);
        var existing = await _db.Set<Appointment>().AsNoTracking()
            .Where(a => a.ScheduledAt >= dayStart && a.ScheduledAt <= dayEnd
                     && a.Status != AppointmentStatus.Cancelled)
            .ToListAsync(ct);

        var slots = new List<AvailabilitySlotDto>();

        foreach (var rule in rules)
        {
            var mechId = rule.MechanicMembershipId;

            // Verificar si hay excepción de bloqueo para este mecánico o para todo el taller.
            var blocked = exceptions.Any(e =>
                e.IsBlocked &&
                (e.MechanicMembershipId == null || e.MechanicMembershipId == mechId));

            if (blocked) continue;

            // Horario efectivo: puede haber una excepción con horario especial.
            var specialRule = exceptions.FirstOrDefault(e =>
                !e.IsBlocked &&
                (e.MechanicMembershipId == null || e.MechanicMembershipId == mechId));

            var start = specialRule?.StartTime ?? rule.StartTime;
            var end = specialRule?.EndTime ?? rule.EndTime;

            // Calcular slots libres dentro del horario con granularidad de requiredMinutes.
            var current = start;
            while (current.AddMinutes(requiredMinutes) <= end)
            {
                var slotStart = new DateTimeOffset(
                    date.ToDateTime(current, DateTimeKind.Utc), TimeSpan.Zero);
                var slotEnd = slotStart.AddMinutes(requiredMinutes);

                // Verificar que no colisione con turnos existentes del mecánico.
                var busy = existing.Any(a =>
                    a.MechanicMembershipId == mechId &&
                    a.ScheduledAt < slotEnd &&
                    a.ScheduledAt.AddMinutes(a.TotalDurationMinutes) > slotStart);

                if (!busy)
                {
                    var mechName = await _db.Set<Membership>()
                        .Where(m => m.Id == mechId)
                        .Join(_db.Set<User>(), m => m.UserId, u => u.Id, (m, u) => u.DisplayName)
                        .FirstOrDefaultAsync(ct);

                    slots.Add(new AvailabilitySlotDto(mechId, mechName, slotStart, slotEnd));
                }

                current = current.AddMinutes(requiredMinutes);
            }
        }

        return slots.OrderBy(s => s.Start).ThenBy(s => s.MechanicMembershipId).ToList();
    }

    public async Task<Guid?> FindMechanicWithLeastWorkloadAsync(DateOnly date, CancellationToken ct)
    {
        var dayOfWeek = date.DayOfWeek;

        var mechanics = await _db.Set<AvailabilityRule>().AsNoTracking()
            .Where(r => r.DayOfWeek == dayOfWeek)
            .Select(r => r.MechanicMembershipId)
            .Distinct()
            .ToListAsync(ct);

        if (mechanics.Count == 0) return null;

        var dayStart = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), TimeSpan.Zero);
        var dayEnd = new DateTimeOffset(date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc), TimeSpan.Zero);

        var workloads = await _db.Set<Appointment>().AsNoTracking()
            .Where(a => a.ScheduledAt >= dayStart && a.ScheduledAt <= dayEnd
                     && a.Status != AppointmentStatus.Cancelled
                     && a.MechanicMembershipId != null)
            .GroupBy(a => a.MechanicMembershipId)
            .Select(g => new { MechanicId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // El mecánico con menos turnos ese día; desempate por ID (determinístico).
        return mechanics
            .OrderBy(m => workloads.FirstOrDefault(w => w.MechanicId == m)?.Count ?? 0)
            .ThenBy(m => m)
            .FirstOrDefault();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken ct) =>
        _db.Set<Customer>().AnyAsync(c => c.Id == customerId, ct);

    public Task<bool> VehicleExistsAsync(Guid vehicleId, CancellationToken ct) =>
        _db.Set<Vehicle>().AnyAsync(v => v.Id == vehicleId, ct);

    public Task<bool> MembershipExistsAsync(Guid membershipId, CancellationToken ct) =>
        _db.Set<Membership>().AnyAsync(m => m.Id == membershipId, ct);

    public async Task<CatalogItemInfo?> GetCatalogItemInfoAsync(Guid catalogItemId, CancellationToken ct)
    {
        var item = await _db.Set<CatalogItem>().AsNoTracking()
            .Where(c => c.Id == catalogItemId && !c.IsDeleted)
            .Select(c => new { c.Name, c.DurationMinutes, c.BufferMinutes })
            .FirstOrDefaultAsync(ct);

        if (item is null) return null;
        return new CatalogItemInfo(item.Name, item.DurationMinutes, item.BufferMinutes);
    }

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
