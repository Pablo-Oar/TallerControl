using TallerControl.Domain.Users;

namespace TallerControl.Application.Registration;

/// <summary>
/// Caso de uso: verificar email desde el link recibido (D-22).
/// El token solo puede usarse una vez y vence a las 24 h.
/// </summary>
public sealed class VerifyEmailHandler
{
    private readonly IRegistrationRepository _repo;
    private readonly TimeProvider _clock;

    public VerifyEmailHandler(IRegistrationRepository repo, TimeProvider clock)
    {
        _repo = repo;
        _clock = clock;
    }

    public async Task<VerifyEmailResponse> HandleAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new VerifyEmailResponse(false, "Token inválido.");
        }

        byte[] hash;
        try
        {
            hash = EmailVerificationToken.HashToken(token);
        }
        catch
        {
            return new VerifyEmailResponse(false, "Token inválido.");
        }

        var record = await _repo.FindVerificationTokenByHashAsync(hash, ct);

        if (record is null)
        {
            return new VerifyEmailResponse(false, "El enlace no existe o ya fue utilizado.");
        }

        var now = _clock.GetUtcNow();

        if (record.IsExpired(now))
        {
            return new VerifyEmailResponse(false, "El enlace venció. Registrate nuevamente.");
        }

        if (record.IsUsed)
        {
            return new VerifyEmailResponse(false, "El enlace ya fue utilizado. Podés iniciar sesión.");
        }

        var user = await _repo.FindUserByIdAsync(record.UserId, ct);
        if (user is null)
        {
            return new VerifyEmailResponse(false, "No se encontró la cuenta.");
        }

        record.MarkUsed(now);
        user.MarkEmailVerified();
        await _repo.SaveChangesAsync(ct);

        return new VerifyEmailResponse(true, "Email verificado correctamente. Ya podés iniciar sesión.");
    }
}
