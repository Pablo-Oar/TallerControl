using Microsoft.EntityFrameworkCore;
using TallerControl.Application.PublicLinks;
using TallerControl.Domain.PublicLinks;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.PublicLinks;

internal sealed class EfPublicLinkRepository : IPublicLinkRepository
{
    private readonly AppDbContext _db;

    public EfPublicLinkRepository(AppDbContext db) => _db = db;

    public Task<PublicLink?> FindByTokenHashAsync(byte[] tokenHash, CancellationToken ct) =>
        _db.Set<PublicLink>()
            .IgnoreQueryFilters()   // sin filtro de tenant — la búsqueda es anónima por hash
            .FirstOrDefaultAsync(pl => pl.TokenHash == tokenHash, ct);

    public async Task<IReadOnlyList<PublicLink>> FindActiveByTargetAsync(
        PublicLinkTargetType targetType, Guid targetId, DateTimeOffset now, CancellationToken ct)
    {
        return await _db.Set<PublicLink>()
            .Where(pl => pl.TargetType == targetType
                && pl.TargetId == targetId
                && pl.RevokedAt == null
                && (pl.ExpiresAt == null || pl.ExpiresAt > now))
            .ToListAsync(ct);
    }

    public async Task AddAsync(PublicLink link, CancellationToken ct) =>
        await _db.Set<PublicLink>().AddAsync(link, ct);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
