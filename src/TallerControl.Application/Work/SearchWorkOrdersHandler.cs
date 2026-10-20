using TallerControl.Application.Common;
using TallerControl.Domain.Work;

namespace TallerControl.Application.Work;

/// <summary>Caso de uso: listar órdenes con filtros (doc 05 §2.7, T-WO-01, T-PERM-03).</summary>
public sealed class SearchWorkOrdersHandler
{
    private readonly IWorkOrderRepository _repo;
    private readonly ITenantContext _tenantContext;

    public SearchWorkOrdersHandler(IWorkOrderRepository repo, ITenantContext tenantContext)
    {
        _repo = repo;
        _tenantContext = tenantContext;
    }

    public async Task<Page<WorkOrderListDto>> HandleAsync(
        WorkOrderStatus? status,
        Guid? mechanicId,
        Guid? vehicleId,
        bool? withBalance,
        DateTimeOffset? from,
        DateTimeOffset? to,
        AssignmentFilter assignment,
        bool isMechanic,
        Guid? actorMembershipId,
        bool includePrices,
        int page,
        int pageSize,
        SortSpec sort,
        CancellationToken ct)
    {
        _ = _tenantContext.TenantId ?? throw new InvalidOperationException("No hay taller en la sesión.");

        // El mecánico solo ve sus órdenes o las sin asignar; el servidor fuerza el filtro (T-PERM-03).
        bool mechanicFilterForced = isMechanic;
        Guid? forcedMechanicId = isMechanic ? actorMembershipId : null;

        return await _repo.SearchAsync(
            status, mechanicId, vehicleId, withBalance, from, to,
            mechanicFilterForced, forcedMechanicId, assignment,
            page, pageSize, includePrices, sort, ct);
    }
}
