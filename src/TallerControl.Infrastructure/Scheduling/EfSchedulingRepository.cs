using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Common;
using TallerControl.Application.Scheduling;
using TallerControl.Domain.Catalog;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Scheduling;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Users;
using TallerControl.Domain.Vehicles;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Scheduling;

internal sealed class EfSchedulingRepository : ISchedulingRepository
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private TimeZoneInfo? _zone;

    public EfSchedulingRepository(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    // ── Zona horaria del taller ───────────────────────────────────────────────
    // Los horarios de atención son «hora de pared» del taller (09:00 es 09:00 en Argentina), así que se convierten
    // a UTC con la zona del taller; los turnos se guardan en UTC.

    private async Task<TimeZoneInfo> ZoneAsync(CancellationToken ct) =>
        _zone ??= await LoadZoneAsync(_tenantContext.TenantId, ct);

    private async Task<TimeZoneInfo> LoadZoneAsync(Guid? tenantId, CancellationToken ct)
    {
        var name = await _db.Set<Tenant>().AsNoTracking().IgnoreQueryFilters()
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

    private static DateTimeOffset LocalToUtc(DateOnly date, TimeOnly time, TimeZoneInfo zone) =>
        new(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified), zone), TimeSpan.Zero);

    private static DateOnly LocalDay(DateTimeOffset at, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);

    public async Task<string> GetTenantTimeZoneIdAsync(CancellationToken ct) => (await ZoneAsync(ct)).Id;

    public async Task<Page<AppointmentListDto>> SearchAsync(
        DateOnly? from, DateOnly? to, Guid? mechanicMembershipId, AppointmentStatus? status,
        int page, int pageSize, CancellationToken ct)
    {
        var zone = await ZoneAsync(ct);
        var query = _db.Set<Appointment>().AsNoTracking()
            .Join(_db.Set<Customer>().AsNoTracking(), a => a.CustomerId, c => c.Id,
                (a, c) => new { Appt = a, CustomerName = c.Name })
            .Join(_db.Set<Vehicle>().AsNoTracking(), x => x.Appt.VehicleId, v => v.Id,
                (x, v) => new { x.Appt, x.CustomerName, VehiclePlate = v.Plate });

        if (from.HasValue)
        {
            var startUtc = LocalToUtc(from.Value, TimeOnly.MinValue, zone);
            query = query.Where(x => x.Appt.ScheduledAt >= startUtc);
        }

        if (to.HasValue)
        {
            var endUtc = LocalToUtc(to.Value.AddDays(1), TimeOnly.MinValue, zone);
            query = query.Where(x => x.Appt.ScheduledAt < endUtc);
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

        // En serie: un DbContext no admite consultas concurrentes.
        var serviceDtos = new List<AppointmentServiceDto>();
        foreach (var s in appt.Services)
        {
            var item = await _db.Set<CatalogItem>().AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == s.CatalogItemId, ct);
            serviceDtos.Add(new AppointmentServiceDto(s.CatalogItemId, item?.Name ?? string.Empty, s.DurationMinutes, s.BufferMinutes));
        }

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
        var zone = await ZoneAsync(ct);
        var dayOfWeek = date.DayOfWeek;

        // Reglas activas para ese día de la semana.
        var rules = await _db.Set<AvailabilityRule>().AsNoTracking()
            .Where(r => r.DayOfWeek == dayOfWeek)
            .ToListAsync(ct);

        // Excepciones del día (bloqueos o cambios de horario).
        var exceptions = await _db.Set<AvailabilityException>().AsNoTracking()
            .Where(e => e.Date == date)
            .ToListAsync(ct);

        // Turnos ya agendados ese día (día calendario del taller).
        var dayStart = LocalToUtc(date, TimeOnly.MinValue, zone);
        var dayEnd = LocalToUtc(date.AddDays(1), TimeOnly.MinValue, zone);
        var existing = await _db.Set<Appointment>().AsNoTracking()
            .Where(a => a.ScheduledAt >= dayStart.AddHours(-12) && a.ScheduledAt < dayEnd
                     && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.Completed)
            .ToListAsync(ct);

        var names = await MechanicNamesAsync(rules.Select(r => r.MechanicMembershipId).Distinct().ToList(), ct);
        var slots = new List<AvailabilitySlotDto>();

        foreach (var rule in rules)
        {
            var mechId = rule.MechanicMembershipId;
            var window = EffectiveWindow(rule, exceptions);
            if (window is null) continue;

            // Slots libres dentro del horario con granularidad de requiredMinutes.
            var current = window.Value.Start;
            while (current.AddMinutes(requiredMinutes) <= window.Value.End)
            {
                var slotStart = LocalToUtc(date, current, zone);
                var slotEnd = slotStart.AddMinutes(requiredMinutes);

                var busy = existing.Any(a =>
                    a.MechanicMembershipId == mechId &&
                    a.ScheduledAt < slotEnd &&
                    a.ScheduledAt.AddMinutes(a.TotalDurationMinutes) > slotStart);

                if (!busy)
                    slots.Add(new AvailabilitySlotDto(mechId, names.GetValueOrDefault(mechId), slotStart, slotEnd));

                current = current.AddMinutes(requiredMinutes);
            }
        }

        return slots.OrderBy(s => s.Start).ThenBy(s => s.MechanicMembershipId).ToList();
    }

    /// <summary>Horario efectivo del mecánico ese día: el de la regla, el de una excepción con horario especial, o ninguno si está bloqueado.</summary>
    private static (TimeOnly Start, TimeOnly End)? EffectiveWindow(AvailabilityRule rule, List<AvailabilityException> dayExceptions)
    {
        var mechId = rule.MechanicMembershipId;
        if (dayExceptions.Any(e => e.IsBlocked && (e.MechanicMembershipId == null || e.MechanicMembershipId == mechId)))
            return null;

        var special = dayExceptions.FirstOrDefault(e =>
            !e.IsBlocked && (e.MechanicMembershipId == null || e.MechanicMembershipId == mechId));

        return (special?.StartTime ?? rule.StartTime, special?.EndTime ?? rule.EndTime);
    }

    private async Task<Dictionary<Guid, string>> MechanicNamesAsync(IReadOnlyCollection<Guid> membershipIds, CancellationToken ct) =>
        await _db.Set<Membership>().AsNoTracking()
            .Where(m => membershipIds.Contains(m.Id))
            .Join(_db.Set<User>().AsNoTracking(), m => m.UserId, u => u.Id, (m, u) => new { m.Id, u.DisplayName })
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);

    public async Task<Guid?> FindMechanicWithLeastWorkloadAsync(DateOnly date, CancellationToken ct)
    {
        var zone = await ZoneAsync(ct);
        var dayOfWeek = date.DayOfWeek;

        var mechanics = await _db.Set<AvailabilityRule>().AsNoTracking()
            .Where(r => r.DayOfWeek == dayOfWeek)
            .Select(r => r.MechanicMembershipId)
            .Distinct()
            .ToListAsync(ct);

        if (mechanics.Count == 0) return null;

        var workloads = await WorkloadsAsync(date, zone, ct);

        // El mecánico con menos turnos ese día; desempate por ID (determinístico).
        return mechanics
            .OrderBy(m => workloads.GetValueOrDefault(m))
            .ThenBy(m => m)
            .FirstOrDefault();
    }

    private async Task<Dictionary<Guid, int>> WorkloadsAsync(DateOnly date, TimeZoneInfo zone, CancellationToken ct)
    {
        var dayStart = LocalToUtc(date, TimeOnly.MinValue, zone);
        var dayEnd = LocalToUtc(date.AddDays(1), TimeOnly.MinValue, zone);

        var rows = await _db.Set<Appointment>().AsNoTracking()
            .Where(a => a.ScheduledAt >= dayStart && a.ScheduledAt < dayEnd
                     && a.Status != AppointmentStatus.Cancelled
                     && a.MechanicMembershipId != null)
            .GroupBy(a => a.MechanicMembershipId!.Value)
            .Select(g => new { MechanicId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.MechanicId, r => r.Count);
    }

    // ── A-05: validación de horario y solapamiento ───────────────────────────

    public async Task<SlotConflict?> CheckSlotAsync(
        Guid mechanicId, DateTimeOffset start, int minutes, Guid? excludeAppointmentId, CancellationToken ct)
    {
        var zone = await ZoneAsync(ct);
        var end = start.AddMinutes(minutes);
        var localDay = LocalDay(start, zone);

        // Horario: solo se exige si el mecánico tiene alguno cargado (sin horarios configurados, no se restringe).
        var rules = await _db.Set<AvailabilityRule>().AsNoTracking()
            .Where(r => r.MechanicMembershipId == mechanicId)
            .ToListAsync(ct);

        if (rules.Count > 0)
        {
            var dayExceptions = await _db.Set<AvailabilityException>().AsNoTracking()
                .Where(e => e.Date == localDay)
                .ToListAsync(ct);

            var blocked = dayExceptions.FirstOrDefault(e =>
                e.IsBlocked && (e.MechanicMembershipId == null || e.MechanicMembershipId == mechanicId));
            if (blocked is not null)
                return new SlotConflict("Ese día está bloqueado" + (string.IsNullOrWhiteSpace(blocked.Reason) ? "." : $" ({blocked.Reason})."));

            var rule = rules.FirstOrDefault(r => r.DayOfWeek == localDay.DayOfWeek);
            if (rule is null)
                return new SlotConflict("El mecánico no atiende ese día de la semana.");

            var window = EffectiveWindow(rule, dayExceptions)!.Value;
            var winStart = LocalToUtc(localDay, window.Start, zone);
            var winEnd = LocalToUtc(localDay, window.End, zone);
            if (start < winStart || end > winEnd)
                return new SlotConflict($"Queda fuera del horario del mecánico ({window.Start:HH\\:mm} a {window.End:HH\\:mm}).");
        }

        // Solapamiento con otros turnos abiertos del mismo mecánico.
        var others = await _db.Set<Appointment>().AsNoTracking()
            .Where(a => a.MechanicMembershipId == mechanicId
                     && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.Completed
                     && a.ScheduledAt < end && a.ScheduledAt >= start.AddHours(-24)
                     && (excludeAppointmentId == null || a.Id != excludeAppointmentId))
            .ToListAsync(ct);

        var overlap = others.FirstOrDefault(a => a.ScheduledAt.AddMinutes(a.TotalDurationMinutes) > start);
        if (overlap is not null)
        {
            var from = TimeZoneInfo.ConvertTime(overlap.ScheduledAt, zone);
            var to = from.AddMinutes(overlap.TotalDurationMinutes);
            return new SlotConflict($"El mecánico ya tiene un turno de {from:HH\\:mm} a {to:HH\\:mm}.");
        }

        return null;
    }

    public async Task<AvailabilitySlotDto?> FindNextFreeSlotAsync(
        Guid? mechanicId, DateTimeOffset from, int minutes, CancellationToken ct)
    {
        var zone = await ZoneAsync(ct);
        var firstDay = LocalDay(from, zone);

        for (var offset = 0; offset < 14; offset++)
        {
            var slots = await GetAvailabilitySlotsAsync(firstDay.AddDays(offset), minutes, ct);
            var next = slots.FirstOrDefault(s => s.Start >= from && (mechanicId is null || s.MechanicMembershipId == mechanicId));
            if (next is not null) return next;
        }

        return null;
    }

    public async Task<(Guid? MechanicId, SlotConflict? Conflict)> PickMechanicAsync(
        DateTimeOffset start, int minutes, CancellationToken ct)
    {
        var zone = await ZoneAsync(ct);
        var localDay = LocalDay(start, zone);

        // Mecánicos con horario cargado ese día de la semana.
        var mechanics = await _db.Set<AvailabilityRule>().AsNoTracking()
            .Where(r => r.DayOfWeek == localDay.DayOfWeek)
            .Select(r => r.MechanicMembershipId)
            .Distinct()
            .ToListAsync(ct);

        if (mechanics.Count == 0) return (null, null);

        var workloads = await WorkloadsAsync(localDay, zone, ct);
        SlotConflict? last = null;

        foreach (var id in mechanics.OrderBy(m => workloads.GetValueOrDefault(m)).ThenBy(m => m))
        {
            var conflict = await CheckSlotAsync(id, start, minutes, null, ct);
            if (conflict is null) return (id, null);
            last = conflict;
        }

        return (null, last ?? new SlotConflict("Ningún mecánico está libre en ese horario."));
    }

    public async Task<CustomerContact?> GetCustomerContactAsync(Guid customerId, CancellationToken ct) =>
        await _db.Set<Customer>().AsNoTracking()
            .Where(c => c.Id == customerId)
            .Select(c => new CustomerContact(c.Name, c.Email, c.DoNotContact))
            .FirstOrDefaultAsync(ct);

    public async Task<PublicAppointmentDto?> GetPublicSummaryAsync(byte[] tokenHash, CancellationToken ct)
    {
        var appt = await _db.Set<Appointment>().AsNoTracking().IgnoreQueryFilters()
            .Include(a => a.Services)
            .FirstOrDefaultAsync(a => a.ConfirmationTokenHash != null && a.ConfirmationTokenHash == tokenHash, ct);

        if (appt is null || appt.Status is not (AppointmentStatus.Pending or AppointmentStatus.Confirmed))
            return null;

        var zone = await LoadZoneAsync(appt.TenantId, ct);
        var vehicle = await _db.Set<Vehicle>().AsNoTracking().IgnoreQueryFilters()
            .Where(v => v.Id == appt.VehicleId)
            .Select(v => new { v.Brand, v.Model })
            .FirstOrDefaultAsync(ct);

        var ids = appt.Services.Select(s => s.CatalogItemId).ToList();
        var serviceNames = await _db.Set<CatalogItem>().AsNoTracking().IgnoreQueryFilters()
            .Where(c => ids.Contains(c.Id))
            .Select(c => c.Name)
            .ToListAsync(ct);

        var label = vehicle is null ? null : $"{vehicle.Brand} {vehicle.Model}".Trim();
        return new PublicAppointmentDto(
            appt.ScheduledAt, zone.Id, appt.TotalDurationMinutes, appt.Status,
            string.IsNullOrEmpty(label) ? null : label, serviceNames);
    }

    public async Task<IReadOnlyList<AvailabilityExceptionDto>> GetExceptionsRangeAsync(
        DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await _db.Set<AvailabilityException>().AsNoTracking()
            .Where(e => e.Date >= from && e.Date <= to)
            .OrderBy(e => e.Date)
            .ToListAsync(ct);

        var names = await MechanicNamesAsync(
            rows.Where(e => e.MechanicMembershipId.HasValue).Select(e => e.MechanicMembershipId!.Value).Distinct().ToList(), ct);

        return rows.Select(e => new AvailabilityExceptionDto(
            e.Id, e.MechanicMembershipId,
            e.MechanicMembershipId.HasValue ? names.GetValueOrDefault(e.MechanicMembershipId.Value) : null,
            e.Date, e.IsBlocked, e.StartTime, e.EndTime, e.Reason)).ToList();
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
            .Where(c => c.Id == catalogItemId)
            .Select(c => new { c.Name, c.DurationMinutes, c.BufferMinutes })
            .FirstOrDefaultAsync(ct);

        if (item is null) return null;
        return new CatalogItemInfo(item.Name, item.DurationMinutes, item.BufferMinutes);
    }

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
