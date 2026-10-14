using System.Security.Cryptography;
using System.Text;
using TallerControl.Application.Common;
using TallerControl.Domain.Qr;

namespace TallerControl.Application.Qr;

public sealed class ValidateOwnerOtpHandler(
    IOwnerVerificationRepository verifications,
    IQrRepository repo,
    TimeProvider clock)
{
    public async Task<QrSession> HandleAsync(
        string code,
        ValidateOwnerOtpRequest request,
        CancellationToken ct)
    {
        var verification = await verifications.FindPendingByChallengeAsync(request.ChallengeId, ct);

        if (verification is null || verification.IsConsumed() || verification.IsExpired(clock.GetUtcNow()))
            throw new BusinessRuleException("El desafío no es válido o expiró.");

        if (verification.IsBlocked())
            throw new BusinessRuleException("Se superó el máximo de intentos. Iniciá un nuevo desafío.");

        var otpHash = SHA256.HashData(Encoding.UTF8.GetBytes(request.Otp.ToUpperInvariant()));
        var valid = CryptographicOperations.FixedTimeEquals(otpHash, verification.OtpHash);

        verification.RecordAttempt();

        if (!valid)
        {
            await verifications.SaveChangesAsync(ct);
            throw new BusinessRuleException("Código incorrecto.");
        }

        var now = clock.GetUtcNow();
        verification.Consume(now);

        // Verificar que la tarjeta sigue activa en el momento de emitir la sesión
        var card = await repo.FindByIdPublicAsync(verification.QrCardId, ct);
        if (card is null || card.Status == QrCardStatus.Revoked || card.VehicleId is null)
        {
            await verifications.SaveChangesAsync(ct);
            throw new BusinessRuleException("La tarjeta ya no está activa.");
        }

        await verifications.SaveChangesAsync(ct);

        return new QrSession(
            QrCardId: card.Id,
            VehicleId: card.VehicleId.Value,
            CustomerId: verification.CustomerId,
            TenantId: verification.TenantId,
            IssuedAt: now);
    }
}
