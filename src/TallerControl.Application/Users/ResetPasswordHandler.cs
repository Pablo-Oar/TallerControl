using TallerControl.Application.Auth;
using TallerControl.Application.Common;
using TallerControl.Domain.Users;

namespace TallerControl.Application.Users;

/// <summary>
/// Caso de uso: restablecer contraseña con token (doc 05 §2, T-AUTH-08).
/// Token de uso único, 1 h de validez. 422 si vencido o ya usado.
/// </summary>
public sealed class ResetPasswordHandler
{
    private readonly IAuthRepository _auth;
    private readonly IPasswordResetRepository _resets;
    private readonly IPasswordHasher _hasher;
    private readonly TimeProvider _clock;

    public ResetPasswordHandler(
        IAuthRepository auth,
        IPasswordResetRepository resets,
        IPasswordHasher hasher,
        TimeProvider clock)
    {
        _auth = auth;
        _resets = resets;
        _hasher = hasher;
        _clock = clock;
    }

    public async Task HandleAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        if (request.NewPassword.Length < User.MinPasswordLength)
        {
            throw new BusinessRuleException(
                $"La contraseña debe tener al menos {User.MinPasswordLength} caracteres.",
                new Dictionary<string, string[]>
                {
                    ["newPassword"] = [$"Mínimo {User.MinPasswordLength} caracteres."],
                });
        }

        var tokenHash = PasswordReset.HashToken(request.Token);
        var reset = await _resets.FindByTokenHashAsync(tokenHash, ct);

        var now = _clock.GetUtcNow();

        if (reset is null || reset.IsExpired(now) || reset.IsUsed)
        {
            throw new BusinessRuleException("El enlace de restablecimiento no es válido o ya expiró.");
        }

        var user = await _auth.FindUserByEmailAsync(reset.Email, ct);
        if (user is null || user.Status != UserStatus.Active)
        {
            throw new BusinessRuleException("El enlace de restablecimiento no es válido o ya expiró.");
        }

        reset.MarkUsed(now);
        user.SetPasswordHash(_hasher.Hash(request.NewPassword));

        await _resets.SaveChangesAsync(ct);
    }
}
