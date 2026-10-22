using TallerControl.Application.Common;
using TallerControl.Domain.Work;

namespace TallerControl.Application.Work;

public interface IWorkOrderRepository
{
    Task<Page<WorkOrderListDto>> SearchAsync(
        WorkOrderStatus? status,
        Guid? mechanicId,
        Guid? vehicleId,
        bool? withBalance,
        DateTimeOffset? from,
        DateTimeOffset? to,
        bool mechanicFilterForced,
        Guid? forcedMechanicId,
        AssignmentFilter assignment,
        int page,
        int pageSize,
        bool includePrices,
        SortSpec sort,
        CancellationToken ct);

    Task<WorkOrderDto?> GetFullAsync(Guid id, bool includePrices, CancellationToken ct);

    /// <summary>
    /// Elimina un renglón que se quitó de una orden editable (el renglón se reemplaza al editar; el historial queda en
    /// los movimientos de stock y en la auditoría). Sin esto EF rechaza el guardado por la relación obligatoria.
    /// </summary>
    void DeleteItem(Domain.Work.WorkOrderItem item);

    Task<WorkOrder?> FindByIdAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Busca una orden que el mecánico pueda ver: la suya o sin asignar.
    /// Devuelve null si la orden es de otro mecánico (→ 404, no 403, REQ-14).
    /// </summary>
    Task<WorkOrder?> FindVisibleToMechanicAsync(Guid id, Guid mechanicMembershipId, CancellationToken ct);

    Task<IReadOnlyList<StatusChangeDto>> GetHistoryAsync(Guid workOrderId, CancellationToken ct);

    Task<Guid?> GetMainBranchIdAsync(Guid tenantId, CancellationToken ct);

    Task AddAsync(WorkOrder order, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);

    Task<IWorkOrderTransaction> BeginTransactionAsync(CancellationToken ct);
}
