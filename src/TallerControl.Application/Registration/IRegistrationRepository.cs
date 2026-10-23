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

    Task<Domain.Users.User?> FindUserByEmailAsync(string email, CancellationToken ct);

    /// <summary>Vencimiento del último enlace emitido (emisión = vencimiento − vigencia) de verificación del usuario (para no reenviar en ráfaga).</summary>
    Task<DateTimeOffset?> LastVerificationTokenAtAsync(Guid userId, CancellationToken ct);

    void AddVerificationToken(EmailVerificationToken token);

    Task SaveChangesAsync(CancellationToken ct);
}
