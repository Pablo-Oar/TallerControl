using TallerControl.Application.Common;
using TallerControl.Domain.Users;

namespace TallerControl.Application.Auth;

public sealed record UpdateProfileRequest(string DisplayName);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>Resultado del cambio de contraseña: el sello nuevo para volver a emitir la cookie de esta sesión.</summary>
public sealed record ChangePasswordResult(string SecurityStamp);

/// <summary>U-02: «Mi perfil» — el usuario edita su propio nombre.</summary>
public sealed class UpdateProfileHandler(IAuthRepository repository)
{
    public async Task HandleAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await repository.FindUserByIdAsync(userId, ct) ?? throw new NotFoundException();

        try
        {
            user.Rename(request.DisplayName);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await repository.SaveChangesAsync(ct);
    }
}

/// <summary>
/// U-02: cambiar la contraseña estando con sesión. Pide la actual (si se equivoca cuenta como intento fallido, así una
/// sesión robada no sirve para adivinarla) y, al cambiarla, cierra las demás sesiones (rota el sello de seguridad).
/// </summary>
public sealed class ChangePasswordHandler(IAuthRepository repository, IPasswordHasher hasher, TimeProvider clock)
{
    public async Task<ChangePasswordResult> HandleAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await repository.FindUserByIdAsync(userId, ct) ?? throw new NotFoundException();
        var now = clock.GetUtcNow();

        if (user.IsLockedOut(now))
            throw new BusinessRuleException("Demasiados intentos fallidos: probá de nuevo en unos minutos.");

        if (user.PasswordHash is null || hasher.Verify(user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            user.RecordFailedLogin(now);
            await repository.SaveChangesAsync(ct);
            throw new BusinessRuleException(
                "La contraseña actual no es correcta.",
                new Dictionary<string, string[]> { ["currentPassword"] = ["No coincide con tu contraseña actual."] });
        }

        if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < User.MinPasswordLength)
        {
            throw new BusinessRuleException(
                $"La contraseña nueva debe tener al menos {User.MinPasswordLength} caracteres.",
                new Dictionary<string, string[]> { ["newPassword"] = [$"Mínimo {User.MinPasswordLength} caracteres."] });
        }

        if (request.NewPassword == request.CurrentPassword)
            throw new BusinessRuleException("La contraseña nueva tiene que ser distinta de la actual.");

        user.ResetAccessFailed();
        user.SetPasswordHash(hasher.Hash(request.NewPassword));
        user.RotateSecurityStamp();
        await repository.SaveChangesAsync(ct);

        return new ChangePasswordResult(user.SecurityStamp);
    }
}
