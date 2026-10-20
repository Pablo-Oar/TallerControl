using TallerControl.Application.Common;

namespace TallerControl.Application.Vehicles;

/// <summary>
/// Caso de uso: historial del vehículo (doc 05 §2.3, REQ-07, T-VEH-08): órdenes y presupuestos por fecha
/// descendente, con km y total. El total solo viaja si el rol puede ver precios (T-PERM-09).
/// </summary>
public sealed class GetVehicleHistoryHandler
{
    private readonly IVehicleRepository _repo;

    public GetVehicleHistoryHandler(IVehicleRepository repo) => _repo = repo;

    public async Task<Page<HistoryEntryDto>> HandleAsync(
        Guid vehicleId, bool includePrices, int page, int pageSize, CancellationToken ct)
    {
        // Un vehículo de otro taller o inexistente es 404 (el filtro global lo oculta).
        _ = await _repo.FindByIdAsync(vehicleId, ct) ?? throw new NotFoundException();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        return await _repo.GetHistoryAsync(vehicleId, includePrices, page, pageSize, ct);
    }
}
