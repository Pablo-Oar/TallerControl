using TallerControl.Domain.Users;

namespace TallerControl.Application.Users;

public interface IPasswordResetRepository
{
    Task<PasswordReset?> FindByTokenHashAsync(byte[] tokenHash, CancellationToken ct);

    void Add(PasswordReset reset);

    Task SaveChangesAsync(CancellationToken ct);
}
