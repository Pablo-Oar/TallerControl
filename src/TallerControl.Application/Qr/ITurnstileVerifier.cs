namespace TallerControl.Application.Qr;

public interface ITurnstileVerifier
{
    Task<bool> VerifyAsync(string token, string? clientIp, CancellationToken ct);
}
