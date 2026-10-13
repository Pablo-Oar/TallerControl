using TallerControl.Application.Auth;
using TallerControl.Application.Common;
using TallerControl.Domain.Users;

namespace TallerControl.Application.Users;

/// <summary>
/// Caso de uso: solicitar restablecimiento de contraseña (doc 05 §2, T-AUTH-07).
/// Siempre responde 202: nunca revela si el email existe (doc 07 §2).
/// El email sale por outbox (D-04).
/// </summary>
public sealed class ForgotPasswordHandler
{
    private const string OutboxType = "email.password_reset";

    private readonly IAuthRepository _auth;
    private readonly IPasswordResetRepository _resets;
    private readonly IOutbox _outbox;
    private readonly TimeProvider _clock;

    public ForgotPasswordHandler(
        IAuthRepository auth,
        IPasswordResetRepository resets,
        IOutbox outbox,
        TimeProvider clock)
    {
        _auth = auth;
        _resets = resets;
        _outbox = outbox;
        _clock = clock;
    }

    public async Task HandleAsync(
        ForgotPasswordRequest request,
        string frontendBaseUrl,
        CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _auth.FindUserByEmailAsync(email, ct);

        // Sin usuario: encolar nada, no revelar existencia.
        if (user is null || user.Status != UserStatus.Active)
        {
            return;
        }

        var now = _clock.GetUtcNow();
        var (reset, token) = PasswordReset.Create(email, now);

        _resets.Add(reset);

        _outbox.Enqueue(OutboxType, new PasswordResetEmailPayload(
            To: email,
            Token: token,
            ExpiresAt: reset.ExpiresAt,
            FrontendBaseUrl: frontendBaseUrl));

        await _resets.SaveChangesAsync(ct);
    }
}

/// <summary>Payload del outbox para el email de restablecimiento de contraseña.</summary>
internal sealed record PasswordResetEmailPayload(
    string To,
    string Token,
    DateTimeOffset ExpiresAt,
    string FrontendBaseUrl);
