using TallerControl.Domain.Users;

namespace TallerControl.Application.Registration;

public interface IRegistrationRepository
{
    Task<bool> IsEmailTakenAsync(string email, CancellationToken ct);

    /// <summary>
    /// Persiste en una sola transacción: Tenant + TenantSettings + Branch + User + Membership
    /// + EmailVerificationToken + document_sequences.
    /// </summary>
    Task CreateTenantWithOwnerAsync(
        RegistrationAggregate aggregate,
        CancellationToken ct);

    Task<EmailVerificationToken?> FindVerificationTokenByHashAsync(
        byte[] tokenHash,
        CancellationToken ct);

    Task<Domain.Users.User?> FindUserByIdAsync(Guid userId, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
