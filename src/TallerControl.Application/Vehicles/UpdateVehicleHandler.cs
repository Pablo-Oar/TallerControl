using TallerControl.Application.Common;

namespace TallerControl.Application.Vehicles;

/// <summary>
/// Caso de uso: editar vehículo (doc 05 §2.3, T-VEH-06..07).
/// Bajar el km requiere el permiso <c>vehicles.odometer.correct</c>; el endpoint lo evalúa y
/// lo pasa como <paramref name="allowOdometerDecrease"/>.
/// </summary>
public sealed class UpdateVehicleHandler
{
    private readonly IVehicleRepository _repo;

    public UpdateVehicleHandler(IVehicleRepository repo) => _repo = repo;

    public async Task<VehicleDto> HandleAsync(
        Guid id,
        uint ifMatchVersion,
        VehicleUpsertRequest request,
        bool allowOdometerDecrease,
        CancellationToken ct)
    {
        var vehicle = await _repo.FindByIdAsync(id, ct)
            ?? throw new NotFoundException();

        if (vehicle.Version != ifMatchVersion)
            throw new ConcurrencyException();

        try
        {
            vehicle.Update(
                request.Brand,
                request.Model,
                request.Version,
                request.Color,
                request.Year,
                request.Vin,
                request.Fuel);

            if (request.OdometerKm != vehicle.OdometerKm)
                vehicle.UpdateOdometer(request.OdometerKm, allowOdometerDecrease);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);

        return await _repo.GetFullAsync(id, ct)
            ?? throw new InvalidOperationException("El vehículo no se pudo leer después de la edición.");
    }
}
