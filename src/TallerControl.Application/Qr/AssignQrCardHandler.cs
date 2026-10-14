using TallerControl.Application.Common;
using TallerControl.Application.Vehicles;

namespace TallerControl.Application.Qr;

public sealed class AssignQrCardHandler(
    IQrRepository repo,
    IVehicleRepository vehicles,
    TimeProvider clock)
{
    public async Task<QrCardDto> HandleAsync(Guid qrCardId, AssignQrCardRequest request, CancellationToken ct)
    {
        var card = await repo.FindByIdAsync(qrCardId, ct)
            ?? throw new NotFoundException("Tarjeta QR no encontrada.");

        if (await vehicles.FindByIdAsync(request.VehicleId, ct) is null)
            throw new NotFoundException("Vehículo no encontrado.");

        var existing = await repo.FindActiveByVehicleAsync(request.VehicleId, ct);
        if (existing is not null && existing.Id != card.Id)
            throw new ConflictException("El vehículo ya tiene una tarjeta activa asignada.");

        card.Assign(request.VehicleId, clock.GetUtcNow());
        await repo.SaveChangesAsync(ct);

        return await repo.GetCardDtoAsync(qrCardId, ct) ?? throw new InvalidOperationException();
    }
}
