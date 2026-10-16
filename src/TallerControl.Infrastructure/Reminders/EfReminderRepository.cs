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
        Guid? vehicleId, int page, int pageSize, CancellationToken ct)
    {
        var query = _db.Set<Reminder>()
            .AsNoTracking()
            .Join(_db.Set<Vehicle>().AsNoTracking(),
                r => r.VehicleId,
                v => v.Id,
                (r, v) => new { Reminder = r, v.Plate });

        if (vehicleId.HasValue)
            query = query.Where(x => x.Reminder.VehicleId == vehicleId.Value);

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
                x.Reminder.Kind.ToString(),
                x.Reminder.Title,
                x.Reminder.DueDate,
                x.Reminder.DueKm,
                x.Reminder.LastNotifiedAt))
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
            r.Kind.ToString(), r.Title, r.Notes,
            r.DueDate, r.DueKm, r.LastNotifiedAt, r.Version);
    }

    public Task<Reminder?> FindByIdAsync(Guid id, CancellationToken ct) =>
        _db.Set<Reminder>().FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<bool> VehicleExistsAsync(Guid vehicleId, CancellationToken ct) =>
        _db.Set<Vehicle>().AnyAsync(v => v.Id == vehicleId, ct);

    public async Task<IReadOnlyList<DueReminderData>> FindDueAsync(
        DateTimeOffset cutoffDate,
        int kmLookahead,
        DateTimeOffset notifiedBefore,
        CancellationToken ct)
    {
        var results = await (
            from r in _db.Set<Reminder>()
            join v in _db.Set<Vehicle>().AsNoTracking() on r.VehicleId equals v.Id
            join c in _db.Set<Customer>().AsNoTracking() on v.CustomerId equals c.Id
            where (r.DueDate.HasValue && r.DueDate <= cutoffDate) ||
                  (r.DueKm.HasValue && v.OdometerKm >= r.DueKm.Value - kmLookahead)
            where r.LastNotifiedAt == null || r.LastNotifiedAt < notifiedBefore
            select new { Reminder = r, v.Plate, c.Name }
        ).ToListAsync(ct);

        return results
            .Select(x => new DueReminderData(x.Reminder, x.Plate, x.Name))
            .ToList();
    }

    public async Task AddAsync(Reminder reminder, CancellationToken ct) =>
        await _db.Set<Reminder>().AddAsync(reminder, ct);

    public async Task AddNotificationAsync(Notification notification, CancellationToken ct) =>
        await _db.Set<Notification>().AddAsync(notification, ct);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
