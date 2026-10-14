using TallerControl.Domain.Common;

namespace TallerControl.Domain.Work;

/// <summary>
/// Registro inmutable de cada transición de estado de una orden. Solo inserción;
/// la base revoca UPDATE/DELETE para el rol de aplicación (doc 04 §3.4).
/// </summary>
public sealed class WorkOrderStatusChange : Entity, ITenantOwned
{
    private WorkOrderStatusChange()
    {
    }

    public WorkOrderStatusChange(
        Guid tenantId,
        Guid workOrderId,
        WorkOrderStatus? fromStatus,
        WorkOrderStatus toStatus,
        string? note,
        Guid? changedBy,
        DateTimeOffset changedAt)
    {
        TenantId = tenantId;
        WorkOrderId = workOrderId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Note = note?.Trim();
        ChangedBy = changedBy;
        ChangedAt = changedAt;
    }

    public Guid TenantId { get; private set; }
    public Guid WorkOrderId { get; private set; }
    public WorkOrderStatus? FromStatus { get; private set; }
    public WorkOrderStatus ToStatus { get; private set; }
    public string? Note { get; private set; }
    public Guid? ChangedBy { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }
}
