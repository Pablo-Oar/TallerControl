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
            // Mecánico: solo puede ver sus propias órdenes o las sin asignar → 404 si es de otro (REQ-14).
            var wo = await _repo.FindVisibleToMechanicAsync(id, mechanicMembershipId.Value, ct)
                ?? throw new NotFoundException();

            return await _repo.GetFullAsync(id, includePrices, ct) ?? throw new NotFoundException();
        }

        return await _repo.GetFullAsync(id, includePrices, ct) ?? throw new NotFoundException();
    }
}
