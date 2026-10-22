using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Notifications;
using TallerControl.Domain.Notifications;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Notifications;

internal sealed class EfNotificationRepository : INotificationRepository
{
    private readonly AppDbContext _db;

    public EfNotificationRepository(AppDbContext db) => _db = db;

    // El filtro global de taller ya limita a los avisos del taller de la sesión.
    public async Task<IReadOnlyList<Notification>> ListForAsync(Guid membershipId, int take, CancellationToken ct) =>
        await _db.Set<Notification>()
            .Where(n => n.MembershipId == null || n.MembershipId == membershipId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

    public Task<int> CountUnreadForAsync(Guid membershipId, CancellationToken ct) =>
        _db.Set<Notification>()
            .Where(n => (n.MembershipId == null || n.MembershipId == membershipId) && n.ReadAt == null)
            .CountAsync(ct);

    public Task<Notification?> FindForAsync(Guid id, Guid membershipId, CancellationToken ct) =>
        _db.Set<Notification>()
            .FirstOrDefaultAsync(n => n.Id == id && (n.MembershipId == null || n.MembershipId == membershipId), ct);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
