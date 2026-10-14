using TallerControl.Application.Common;

namespace TallerControl.Application.Qr;

public sealed class GetQrCardEventsHandler(IQrRepository repo)
{
    public async Task<Page<QrEventDto>> HandleAsync(Guid qrCardId, int page, int pageSize, CancellationToken ct)
    {
        var card = await repo.FindByIdAsync(qrCardId, ct)
            ?? throw new NotFoundException("Tarjeta QR no encontrada.");

        return await repo.GetEventsAsync(card.Id, page, pageSize, ct);
    }
}
