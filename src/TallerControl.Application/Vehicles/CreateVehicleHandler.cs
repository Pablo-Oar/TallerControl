using TallerControl.Application.Common;
using TallerControl.Domain.Vehicles;

namespace TallerControl.Application.Vehicles;

/// <summary>Caso de uso: crear vehículo (doc 05 §2.3, T-VEH-02..04).</summary>
public sealed class CreateVehicleHandler
{
    private readonly IVehicleRepository _repo;
    private readonly ITenantContext _tenantContext;

    public CreateVehicleHandler(IVehicleRepository repo, ITenantContext tenantContext)
    {
        _repo = repo;
        _tenantContext = tenantContext;
    }

    public async Task<VehicleDto> HandleAsync(VehicleUpsertRequest request, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        string normalizedPlate;
        try
        {
            normalizedPlate = Vehicle.NormalizePlate(request.Plate);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        if (!await _repo.CustomerExistsAsync(request.CustomerId, ct))
            throw new NotFoundException();

        if (await _repo.PlateExistsAsync(null, normalizedPlate, ct))
            throw new ConflictException($"Ya existe un vehículo con la patente {normalizedPlate} en este taller.");

        Vehicle vehicle;
        try
        {
            vehicle = new Vehicle(
                tenantId,
                request.CustomerId,
                request.Plate,
                request.Brand,
                request.Model,
                request.Version,
                request.Color,
                request.Year,
                request.Vin,
                request.Fuel,
                request.OdometerKm);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await _repo.AddAsync(vehicle, ct);
        await _repo.SaveChangesAsync(ct);

        return await _repo.GetFullAsync(vehicle.Id, ct)
            ?? throw new InvalidOperationException("El vehículo recién creado no se pudo leer.");
    }
}
