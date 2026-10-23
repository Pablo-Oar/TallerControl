using TallerControl.Application.Common;
using TallerControl.Domain.Users;

namespace TallerControl.Application.Registration;

public sealed record ResendVerificationRequest(string Email);

/// <summary>
/// U-01: reenvía el enlace de verificación (el anterior vence a las 24 h). Responde siempre igual, exista o no el
/// email y esté o no verificado, para no revelar quién tiene cuenta; no reenvía más de una vez por minuto.
/// </summary>
public sealed class ResendVerificationHandler(IRegistrationRepository repo, IOutbox outbox, TimeProvider clock)
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(60);

    public async Task HandleAsync(ResendVerificationRequest request, string frontendBaseUrl, CancellationToken ct)
    {
        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (email.Length == 0) return;

        var user = await repo.FindUserByEmailAsync(email, ct);
        if (user is null || user.IsEmailVerified) return;

        var now = clock.GetUtcNow();
        var lastExpires = await repo.LastVerificationTokenAtAsync(user.Id, ct);
        if (lastExpires is not null && now - (lastExpires.Value - EmailVerificationToken.Validity) < Cooldown) return;

        var (token, plain) = EmailVerificationToken.Create(user.Id, now);
        repo.AddVerificationToken(token);

        outbox.Enqueue("email.email_verification", new EmailVerificationPayload(
            To: email,
            Token: plain,
            ExpiresAt: token.ExpiresAt,
            FrontendBaseUrl: frontendBaseUrl));

        await repo.SaveChangesAsync(ct);
    }
}
