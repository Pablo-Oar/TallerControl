using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Qr;
using TallerControl.Domain.Qr;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Qr;

internal sealed class EfOwnerVerificationRepository : IOwnerVerificationRepository
{
    private readonly AppDbContext _db;

    public EfOwnerVerificationRepository(AppDbContext db)
    {
        _db = db;
    }

    // IgnoreQueryFilters: el challenge se busca por ID en ruta pública (sin tenant en sesión).
    public Task<OwnerVerification?> FindPendingByChallengeAsync(Guid challengeId, CancellationToken ct) =>
        _db.Set<OwnerVerification>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.Id == challengeId && v.ConsumedAt == null, ct);

    public Task<int> CountActiveByQrCardAsync(Guid qrCardId, DateTimeOffset since, CancellationToken ct) =>
        _db.Set<OwnerVerification>()
            .IgnoreQueryFilters()
            .CountAsync(v => v.QrCardId == qrCardId && v.ExpiresAt > since && v.ConsumedAt == null, ct);

    public async Task AddAsync(OwnerVerification verification, CancellationToken ct) =>
        await _db.Set<OwnerVerification>().AddAsync(verification, ct);

    public async Task InvalidateByQrCardAsync(Guid qrCardId, DateTimeOffset now, CancellationToken ct)
    {
        // Marcar como consumidas todas las verificaciones activas de la tarjeta
        var pending = await _db.Set<OwnerVerification>()
            .IgnoreQueryFilters()
            .Where(v => v.QrCardId == qrCardId && v.ConsumedAt == null && v.ExpiresAt > now)
            .ToListAsync(ct);

        foreach (var v in pending)
            v.Consume(now);
    }

    public Task SaveChangesAsync(CancellationToken ct) =>
        _db.SaveChangesAsync(ct);
}
