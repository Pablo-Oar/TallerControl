using TallerControl.Application.Common;

namespace TallerControl.Application.Vehicles;

/// <summary>Caso de uso: buscar vehículos (doc 05 §2.3, T-VEH-01).</summary>
public sealed class SearchVehiclesHandler
{
    private readonly IVehicleRepository _repo;

    public SearchVehiclesHandler(IVehicleRepository repo) => _repo = repo;

    public Task<Page<VehicleListDto>> HandleAsync(string? q, int page, int pageSize, SortSpec sort, bool includeDeleted, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        return _repo.SearchAsync(q, page, pageSize, sort, includeDeleted, ct);
    }
}
