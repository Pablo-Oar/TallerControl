using TallerControl.Application.Common;

namespace TallerControl.Application.Vehicles;

/// <summary>
/// Caso de uso: historial del vehículo (doc 05 §2.3, T-VEH-08).
/// En E3 devuelve vacío; se completa en E4 cuando existan presupuestos y órdenes.
/// </summary>
public sealed class GetVehicleHistoryHandler
{
    private readonly IVehicleRepository _repo;

    public GetVehicleHistoryHandler(IVehicleRepository repo) => _repo = repo;

    public async Task<Page<HistoryEntryDto>> HandleAsync(Guid vehicleId, int page, int pageSize, CancellationToken ct)
    {
        if (!await _repo.FindByIdAsync(vehicleId, ct).ContinueWith(t => t.Result is not null, ct))
            throw new NotFoundException();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        return new Page<HistoryEntryDto>([], page, pageSize, 0);
    }
}
