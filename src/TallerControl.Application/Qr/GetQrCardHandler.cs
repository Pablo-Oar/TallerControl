using TallerControl.Application.Common;

namespace TallerControl.Application.Qr;

public sealed class GetQrCardHandler(IQrRepository repo)
{
    public async Task<QrCardDto> HandleAsync(Guid id, CancellationToken ct)
    {
        return await repo.GetCardDtoAsync(id, ct)
            ?? throw new NotFoundException("Tarjeta QR no encontrada.");
    }
}
