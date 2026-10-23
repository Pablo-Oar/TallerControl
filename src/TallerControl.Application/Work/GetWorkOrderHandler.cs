using TallerControl.Application.Common;

namespace TallerControl.Application.Work;

/// <summary>Caso de uso: ver una orden (doc 05 §2.7, T-WO-05, T-PERM-04, T-PERM-05).</summary>
public sealed class GetWorkOrderHandler
{
    private readonly IWorkOrderRepository _repo;

    public GetWorkOrderHandler(IWorkOrderRepository repo) => _repo = repo;

    public async Task<WorkOrderDto> HandleAsync(
        Guid id,
        bool isMechanic,
        Guid? mechanicMembershipId,
        bool includePrices,
        CancellationToken ct)
    {
        if (isMechanic && mechanicMembershipId.HasValue)
        {
            // Mecánico: ve todas las órdenes del taller (las ajenas solo para leer), sin notas internas ni importes sin permiso.
            return await _repo.GetFullAsync(id, includePrices, ct, includeInternalNotes: false) ?? throw new NotFoundException();
        }

        return await _repo.GetFullAsync(id, includePrices, ct) ?? throw new NotFoundException();
    }
}
