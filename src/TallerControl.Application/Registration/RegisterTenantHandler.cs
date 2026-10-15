using TallerControl.Application.Auth;
using TallerControl.Application.Common;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Users;

namespace TallerControl.Application.Registration;

/// <summary>
/// Caso de uso: auto-registro de un nuevo taller (D-22, E10).
/// Crea en una transacción: Tenant + TenantSettings + Branch principal + User + Membership (Owner)
/// + EmailVerificationToken; encola el email de verificación por outbox.
/// Responde 201 con el email del owner; el login queda bloqueado hasta verificar (D-22).
/// </summary>
public sealed class RegisterTenantHandler
{
    private const string OutboxType = "email.email_verification";

    private readonly IRegistrationRepository _repo;
    private readonly IPasswordHasher _hasher;
    private readonly IOutbox _outbox;
    private readonly TimeProvider _clock;

    public RegisterTenantHandler(
        IRegistrationRepository repo,
        IPasswordHasher hasher,
        IOutbox outbox,
        TimeProvider clock)
    {
        _repo = repo;
        _hasher = hasher;
        _outbox = outbox;
        _clock = clock;
    }

    public async Task<RegisterTenantResponse> HandleAsync(
        RegisterTenantRequest request,
        string frontendBaseUrl,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();

        if (await _repo.IsEmailTakenAsync(email, ct))
        {
            // Respuesta genérica: no revelamos que el email ya existe (doc 07 §2).
            // El usuario recibirá el email de verificación de todos modos si el email es nuevo.
            // Si ya existe, simplemente no hacemos nada — el frontend muestra el mismo mensaje.
            return new RegisterTenantResponse(email);
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < User.MinPasswordLength)
        {
            throw new BusinessRuleException(
                $"La contraseña debe tener al menos {User.MinPasswordLength} caracteres.");
        }

        var now = _clock.GetUtcNow();

        var tenant = new Tenant(request.WorkshopName, now);
        var settings = new TenantSettings(tenant.Id);
        var branch = Branch.CreateMain(tenant.Id);
        var user = User.RegisterSelf(email, request.DisplayName, _hasher.Hash(request.Password));
        var membership = new Membership(tenant.Id, user.Id, TenantRole.Owner);
        var (verificationToken, plainToken) = EmailVerificationToken.Create(user.Id, now);

        var aggregate = new RegistrationAggregate(
            tenant, settings, branch, user, membership, verificationToken);

        _outbox.Enqueue(OutboxType, new EmailVerificationPayload(
            To: email,
            Token: plainToken,
            ExpiresAt: verificationToken.ExpiresAt,
            FrontendBaseUrl: frontendBaseUrl));

        await _repo.CreateTenantWithOwnerAsync(aggregate, ct);

        return new RegisterTenantResponse(email);
    }
}

/// <summary>Payload del outbox para el email de verificación.</summary>
internal sealed record EmailVerificationPayload(
    string To,
    string Token,
    DateTimeOffset ExpiresAt,
    string FrontendBaseUrl);
