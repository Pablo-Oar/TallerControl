using TallerControl.Application.Common;

namespace TallerControl.Application.Qr;

public sealed class UnassignQrCardHandler(IQrRepository repo, IOwnerVerificationRepository verifications, TimeProvider clock)
{
    public async Task HandleAsync(Guid qrCardId, CancellationToken ct)
    {
        var card = await repo.FindByIdAsync(qrCardId, ct)
            ?? throw new NotFoundException("Tarjeta QR no encontrada.");

        card.Unassign();
        await verifications.InvalidateByQrCardAsync(qrCardId, clock.GetUtcNow(), ct);
        await repo.SaveChangesAsync(ct);
    }
}
