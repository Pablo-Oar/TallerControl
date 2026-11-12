using TallerControl.Application.Common;
using TallerControl.Domain.Qr;

namespace TallerControl.Application.Qr;

/// <summary>Detalle de una orden del vehículo para el dueño que entró por QR: qué servicios y repuestos se hicieron.</summary>
public sealed class GetOwnerWorkOrderHandler(IQrRepository repo, TimeProvider clock)
{
    public async Task<OwnerWorkOrderDetailDto> HandleAsync(QrSession session, int number, CancellationToken ct)
    {
        if (clock.GetUtcNow() - session.IssuedAt > TimeSpan.FromMinutes(30))
            throw new BusinessRuleException("La sesión expiró. Escaneá el código QR nuevamente.");

        var card = await repo.FindByIdPublicAsync(session.QrCardId, ct);
        if (card is null || card.Status == QrCardStatus.Revoked || card.VehicleId != session.VehicleId)
            throw new BusinessRuleException("La tarjeta ya no está activa.");

        // Una orden de otro vehículo (aunque sea del mismo taller) es 404, igual que una inexistente.
        return await repo.GetOwnerWorkOrderAsync(session.VehicleId, session.TenantId, number, ct)
            ?? throw new NotFoundException();
    }
}
