using TallerControl.Domain.Users;

namespace TallerControl.Application.Auth;

/// <summary>
/// Caso de uso de login (doc 05 §2, REQ-02). Responsable de:
/// <list type="bullet">
/// <item>Normalizar y validar el email.</item>
/// <item>Verificar la contraseña con PBKDF2 en tiempo constante incluso si el email no existe.</item>
/// <item>Aplicar el bloqueo a los 5 intentos fallidos durante 15 minutos.</item>
/// <item>Resolver la membresía activa para armar los claims de la cookie.</item>
/// </list>
/// El endpoint traduce <see cref="AuthenticationException"/> a 401 y <see cref="AccountLockedException"/> a 423.
/// </summary>
public sealed class LoginHandler
{
    private readonly IAuthRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly TimeProvider _clock;

    public LoginHandler(IAuthRepository repository, IPasswordHasher passwordHasher, TimeProvider clock)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _clock = clock;
    }

    public async Task<LoginResult> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = NormalizeEmail(request.Email);
        var password = request.Password ?? string.Empty;

        var user = await _repository.FindUserByEmailAsync(email, ct);
        var now = _clock.GetUtcNow();

        if (user is null)
        {
            // Rama "usuario inexistente": hasheamos igual para que el tiempo de respuesta no delate
            // la existencia del email (doc 05 §2, T-AUTH-02).
            _ = _passwordHasher.Hash(password);
            throw new AuthenticationException();
        }

        if (user.IsLockedOut(now))
        {
            throw new AccountLockedException(user.LockoutEnd!.Value);
        }

        if (user.Status != UserStatus.Active || string.IsNullOrEmpty(user.PasswordHash))
        {
            _ = _passwordHasher.Hash(password);
            throw new AuthenticationException();
        }

        var verification = _passwordHasher.Verify(user.PasswordHash, password);

        if (verification == PasswordVerificationResult.Failed)
        {
            user.RecordFailedLogin(now);
            await _repository.SaveChangesAsync(ct);
            throw new AuthenticationException();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.SetPasswordHash(_passwordHasher.Hash(password));
        }

        user.ResetAccessFailed();

        var membership = await _repository.FindActiveMembershipAsync(user.Id, ct);
        if (membership is null)
        {
            // Sin membresía activa no hay taller: no se puede autenticar. Mensaje genérico.
            await _repository.SaveChangesAsync(ct);
            throw new AuthenticationException();
        }

        await _repository.SaveChangesAsync(ct);

        return new LoginResult(user.Id, user.SecurityStamp, membership.Tenant.Id, membership.Membership.Role);
    }

    internal static string NormalizeEmail(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();
}

/// <summary>Datos para armar la cookie de sesión tras un login exitoso.</summary>
public sealed record LoginResult(Guid UserId, string SecurityStamp, Guid TenantId, TenantRole Role);
