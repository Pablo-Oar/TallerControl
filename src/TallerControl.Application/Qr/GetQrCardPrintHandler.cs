using TallerControl.Application.Common;

namespace TallerControl.Application.Qr;

public sealed class GetQrCardPrintHandler(IQrRepository repo, IQrPdfGenerator pdf)
{
    public async Task<byte[]> HandleAsync(Guid qrCardId, string format, CancellationToken ct)
    {
        var card = await repo.FindByIdAsync(qrCardId, ct)
            ?? throw new NotFoundException("Tarjeta QR no encontrada.");

        if (format == "card" && card.VehicleId is null)
            throw new ConflictException("Solo se puede imprimir en formato tarjeta una tarjeta asignada.");

        return await pdf.GenerateCardAsync(qrCardId, ct);
    }
}
