using TallerControl.Application.Common;

namespace TallerControl.Application.Vehicles;

/// <summary>Caso de uso: ver ficha del vehículo (doc 05 §2.3, T-VEH-05).</summary>
public sealed class GetVehicleHandler
{
    private readonly IVehicleRepository _repo;

    public GetVehicleHandler(IVehicleRepository repo) => _repo = repo;

    public async Task<VehicleDto> HandleAsync(Guid id, CancellationToken ct)
    {
        return await _repo.GetFullAsync(id, ct) ?? throw new NotFoundException();
    }
}
