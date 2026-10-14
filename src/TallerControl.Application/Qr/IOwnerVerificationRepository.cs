using TallerControl.Domain.Qr;

namespace TallerControl.Application.Qr;

public interface IOwnerVerificationRepository
{
    Task<OwnerVerification?> FindPendingByChallengeAsync(Guid challengeId, CancellationToken ct);
    Task<int> CountActiveByQrCardAsync(Guid qrCardId, DateTimeOffset since, CancellationToken ct);
    Task AddAsync(OwnerVerification verification, CancellationToken ct);
    Task InvalidateByQrCardAsync(Guid qrCardId, DateTimeOffset now, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
