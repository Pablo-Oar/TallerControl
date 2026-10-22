using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Common;
using TallerControl.Application.Reminders;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Notifications;
using TallerControl.Domain.Reminders;
using TallerControl.Domain.Vehicles;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Reminders;

internal sealed class EfReminderRepository : IReminderRepository
{
    private readonly AppDbContext _db;

    public EfReminderRepository(AppDbContext db) => _db = db;

    public async Task<Page<ReminderListDto>> SearchAsync(
        Guid? vehicleId, string status, int page, int pageSize, CancellationToken ct)
    {
        var query = _db.Set<Reminder>()
            .AsNoTracking()
            .Join(_db.Set<Vehicle>().AsNoTracking(),
                r => r.VehicleId,
                v => v.Id,
                (r, v) => new { Reminder = r, v.Plate });

        if (vehicleId.HasValue)
            query = query.Where(x => x.Reminder.VehicleId == vehicleId.Value);

        if (status == "pending")
            query = query.Where(x => x.Reminder.CompletedAt == null);
        else if (status == "completed")
            query = query.Where(x => x.Reminder.CompletedAt != null);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(x => x.Reminder.DueDate)
            .ThenBy(x => x.Reminder.DueKm)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ReminderListDto(
                x.Reminder.Id,
                x.Reminder.VehicleId,
                x.Plate,
                x.Reminder.Kind,
                x.Reminder.Title,
                x.Reminder.DueDate,
                x.Reminder.DueKm,
                x.Reminder.LastNotifiedAt,
                x.Reminder.CompletedAt))
            .ToListAsync(ct);

        return new Page<ReminderListDto>(items, page, pageSize, total);
    }

    public async Task<ReminderDto?> GetFullAsync(Guid id, CancellationToken ct)
    {
        var result = await _db.Set<Reminder>()
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Join(_db.Set<Vehicle>().AsNoTracking(),
                r => r.VehicleId,
                v => v.Id,
                (r, v) => new { Reminder = r, v.Plate })
            .FirstOrDefaultAsync(ct);

        if (result is null) return null;

        var r = result.Reminder;
        return new ReminderDto(
            r.Id, r.VehicleId, result.Plate,
            r.Kind, r.Title, r.Notes,
            r.DueDate, r.DueKm, r.LastNotifiedAt, r.Version, r.CompletedAt);
    }

    public Task<Reminder?> FindByIdAsync(Guid id, CancellationToken ct) =>
        _db.Set<Reminder>().FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<bool> VehicleExistsAsync(Guid vehicleId, CancellationToken ct) =>
        _db.Set<Vehicle>().AnyAsync(v => v.Id == vehicleId, ct);

    public Task<IReadOnlyList<DueReminderData>> FindDueAsync(
        DateTimeOffset cutoffDate, int kmLookahead, DateTimeOffset notifiedBefore, CancellationToken ct) =>
        QueryDueAsync(allTenants: false, cutoffDate, kmLookahead, notifiedBefore, ct);

    public Task<IReadOnlyList<DueReminderData>> FindDueAllTenantsAsync(
        DateTimeOffset cutoffDate, int kmLookahead, DateTimeOffset notifiedBefore, CancellationToken ct) =>
        QueryDueAsync(allTenants: true, cutoffDate, kmLookahead, notifiedBefore, ct);

    private async Task<IReadOnlyList<DueReminderData>> QueryDueAsync(
        bool allTenants,
        DateTimeOffset cutoffDate,
        int kmLookahead,
        DateTimeOffset notifiedBefore,
        CancellationToken ct)
    {
        // La tarea periódica no tiene sesión: se saltan los filtros de taller (cada recordatorio ya trae el suyo).
        var reminders = allTenants ? _db.Set<Reminder>().IgnoreQueryFilters() : _db.Set<Reminder>();
        var vehicles = allTenants ? _db.Set<Vehicle>().IgnoreQueryFilters() : _db.Set<Vehicle>();
        var customers = allTenants ? _db.Set<Customer>().IgnoreQueryFilters() : _db.Set<Customer>();

        // Sin AsNoTracking: basta uno en la consulta para que el recordatorio deje de rastrearse y MarkNotified no se guarde.
        var results = await (
            from r in reminders
            join v in vehicles on r.VehicleId equals v.Id
            join c in customers on v.CustomerId equals c.Id
            where (r.DueDate.HasValue && r.DueDate <= cutoffDate) ||
                  (r.DueKm.HasValue && v.OdometerKm >= r.DueKm.Value - kmLookahead)
            where r.CompletedAt == null
            where r.LastNotifiedAt == null || r.LastNotifiedAt < notifiedBefore
            where v.DeletedAt == null && c.DeletedAt == null
            select new { Reminder = r, v.Plate, c.Name, c.Email, c.DoNotContact }
        ).ToListAsync(ct);

        return results
            .Select(x => new DueReminderData(x.Reminder, x.Plate, x.Name, x.Email, x.DoNotContact))
            .ToList();
    }

    public async Task AddAsync(Reminder reminder, CancellationToken ct) =>
        await _db.Set<Reminder>().AddAsync(reminder, ct);

    public async Task AddNotificationAsync(Notification notification, CancellationToken ct) =>
        await _db.Set<Notification>().AddAsync(notification, ct);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
