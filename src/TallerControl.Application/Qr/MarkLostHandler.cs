using TallerControl.Application.Common;

namespace TallerControl.Application.Qr;

public sealed class MarkLostHandler(IQrRepository repo)
{
    public async Task HandleAsync(Guid qrCardId, MarkLostRequest request, CancellationToken ct)
    {
        var card = await repo.FindByIdAsync(qrCardId, ct)
            ?? throw new NotFoundException("Tarjeta QR no encontrada.");

        if (request.Lost)
            card.MarkLost();
        else
            card.UnmarkLost();

        await repo.SaveChangesAsync(ct);
    }
}
