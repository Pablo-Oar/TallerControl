using TallerControl.Application.Common;

namespace TallerControl.Application.Vehicles;

/// <summary>Caso de uso: restaurar un vehículo borrado lógicamente (REQ-06; simétrico a restaurar cliente).</summary>
public sealed class RestoreVehicleHandler
{
    private readonly IVehicleRepository _repo;

    public RestoreVehicleHandler(IVehicleRepository repo) => _repo = repo;

    public async Task<VehicleDto> HandleAsync(Guid id, CancellationToken ct)
    {
        var vehicle = await _repo.FindAnyByIdAsync(id, ct)
            ?? throw new NotFoundException();

        if (!vehicle.IsDeleted)
            throw new ConflictException("El vehículo no está borrado.");

        // La patente quedó libre al borrarlo: si se volvió a cargar, no se puede restaurar.
        if (await _repo.PlateExistsAsync(id, vehicle.Plate, ct))
            throw new ConflictException(
                $"No se puede restaurar: la patente {vehicle.Plate} ya está en uso por otro vehículo.");

        // Un vehículo no puede quedar activo con un dueño eliminado.
        if (!await _repo.CustomerExistsAsync(vehicle.CustomerId, ct))
            throw new BusinessRuleException("El cliente de este vehículo está eliminado: restauralo primero.");

        vehicle.Restore();
        await _repo.SaveChangesAsync(ct);

        return await _repo.GetFullAsync(id, ct)
            ?? throw new InvalidOperationException("El vehículo restaurado no se pudo leer.");
    }
}
