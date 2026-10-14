using System.Security.Cryptography;
using System.Text;
using TallerControl.Application.Common;
using TallerControl.Domain.Qr;

namespace TallerControl.Application.Qr;

public sealed class StartOwnerChallengeHandler(
    IQrRepository repo,
    IOwnerVerificationRepository verifications,
    ITurnstileVerifier turnstile,
    IOutbox outbox,
    TimeProvider clock)
{
    private static readonly TimeSpan ChallengeValidity = TimeSpan.FromMinutes(10);

    public async Task<OwnerChallengeResult> HandleAsync(
        string code,
        OwnerChallengeRequest request,
        string? clientIp,
        CancellationToken ct)
    {
        // Verificar Turnstile antes de cualquier acceso a datos
        var turnstileOk = await turnstile.VerifyAsync(request.TurnstileToken, clientIp, ct);
        if (!turnstileOk)
            return FakeResult();

        var card = await repo.FindByCodeAsync(code.ToUpperInvariant(), ct);
        if (card is null || card.Status != QrCardStatus.Assigned || card.VehicleId is null)
            return FakeResult();

        var ownerInfo = await repo.GetVehicleOwnerForChallengeAsync(card.VehicleId.Value, ct);
        if (ownerInfo is null)
            return FakeResult();

        // Normalizar la patente del request para comparar
        var normalizedRequestPlate = request.Plate.ToUpperInvariant().Replace("-", "").Replace(" ", "");
        if (ownerInfo.NormalizedPlate != normalizedRequestPlate)
            return FakeResult();

        var now = clock.GetUtcNow();
        string otp;
        string channel;
        string? maskedDestination;

        if (request.CustomerCode is not null)
        {
            // Ruta código de acceso: el código del cliente es el "OTP"
            if (ownerInfo.OwnerAccessCodeHash is null)
                return FakeResult();

            otp = request.CustomerCode.ToUpperInvariant();
            channel = "access_code";
            maskedDestination = null;
        }
        else
        {
            // Ruta email
            if (ownerInfo.Email is null)
                return FakeResult();

            otp = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            channel = "email";
            maskedDestination = MaskEmail(ownerInfo.Email);
        }

        var otpHash = SHA256.HashData(Encoding.UTF8.GetBytes(otp));
        var verification = new OwnerVerification(
            ownerInfo.TenantId,
            card.Id,
            ownerInfo.CustomerId,
            otpHash,
            now.Add(ChallengeValidity));

        await verifications.AddAsync(verification, ct);

        if (channel == "email")
        {
            outbox.Enqueue("QrOtpEmail", new
            {
                To = ownerInfo.Email,
                Otp = otp,
                ExpiresAt = verification.ExpiresAt
            });
        }

        await verifications.SaveChangesAsync(ct);

        return new OwnerChallengeResult(verification.Id, channel, maskedDestination);
    }

    private static OwnerChallengeResult FakeResult() =>
        new(Guid.CreateVersion7(), "email", null);

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1) return "***@***";
        var local = email[..at];
        var domain = email[at..];
        var visible = local.Length > 2 ? local[..2] : local[..1];
        return visible + "***" + domain;
    }
}
