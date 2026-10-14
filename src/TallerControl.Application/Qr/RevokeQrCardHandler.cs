using TallerControl.Application.Common;

namespace TallerControl.Application.Qr;

public sealed class RevokeQrCardHandler(IQrRepository repo, IOwnerVerificationRepository verifications, TimeProvider clock)
{
    public async Task HandleAsync(Guid qrCardId, RevokeQrCardRequest request, CancellationToken ct)
    {
        var card = await repo.FindByIdAsync(qrCardId, ct)
            ?? throw new NotFoundException("Tarjeta QR no encontrada.");

        var now = clock.GetUtcNow();
        card.Revoke(request.Reason, now);
        await verifications.InvalidateByQrCardAsync(qrCardId, now, ct);
        await repo.SaveChangesAsync(ct);
    }
}
