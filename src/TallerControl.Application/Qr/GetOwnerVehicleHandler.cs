using TallerControl.Application.Common;
using TallerControl.Domain.Qr;

namespace TallerControl.Application.Qr;

public sealed class GetOwnerVehicleHandler(IQrRepository repo, TimeProvider clock)
{
    public async Task<OwnerVehicleDto> HandleAsync(QrSession session, CancellationToken ct)
    {
        // Validar que la sesión no expiró (30 minutos desde issuedAt)
        if (clock.GetUtcNow() - session.IssuedAt > TimeSpan.FromMinutes(30))
            throw new BusinessRuleException("La sesión expiró. Escaneá el código QR nuevamente.");

        // Verificar que la tarjeta sigue activa y asignada al mismo vehículo
        var card = await repo.FindByIdPublicAsync(session.QrCardId, ct);
        if (card is null || card.Status == QrCardStatus.Revoked
            || card.VehicleId != session.VehicleId)
            throw new BusinessRuleException("La tarjeta ya no está activa.");

        var dto = await repo.GetOwnerVehicleDtoAsync(session.VehicleId, session.TenantId, ct);
        if (dto is null)
            throw new NotFoundException();

        return dto;
    }
}
