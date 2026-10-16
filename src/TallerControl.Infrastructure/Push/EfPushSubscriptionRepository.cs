using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Push;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Push;

internal sealed class EfPushSubscriptionRepository : IPushSubscriptionRepository
{
    private readonly AppDbContext _db;

    public EfPushSubscriptionRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task UpsertAsync(Guid userId, string endpoint, string p256Dh, string auth, CancellationToken ct)
    {
        // Upsert directo via raw SQL para evitar concurrencia de lectura+escritura.
        await _db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO push_subscriptions (user_id, endpoint, p256dh, auth, created_at)
            VALUES ({userId}, {endpoint}, {p256Dh}, {auth}, now())
            ON CONFLICT (user_id, endpoint) DO UPDATE
                SET p256dh = EXCLUDED.p256dh, auth = EXCLUDED.auth
            """,
            ct);
    }

    public async Task DeleteAsync(Guid userId, string endpoint, CancellationToken ct)
    {
        await _db.Database.ExecuteSqlAsync(
            $"DELETE FROM push_subscriptions WHERE user_id = {userId} AND endpoint = {endpoint}",
            ct);
    }
}
