using TallerControl.Domain.Common;

namespace TallerControl.Domain.Work;

/// <summary>
/// Orden de trabajo. No usa soft-delete: el ciclo de vida termina en Cancelled o Delivered.
/// Los totales se calculan en el servidor (REQ-12, doc 04 §3.4).
/// </summary>
public sealed class WorkOrder : Entity, ITenantOwned, IVersioned, IAudited, IAuditable
{
    public const int ReportedIssueMaxLength = 1000;
    public const int NotesMaxLength = 2000;
    public const int CancelReasonMaxLength = 500;

    // Transiciones válidas por estado (REQ-12, doc 03):
    // Pending       → InProgress, Cancelled
    // InProgress    → WaitingParts, Completed, Cancelled
    // WaitingParts  → InProgress, Cancelled
    // Completed     → Delivered, Cancelled
    // Delivered     → (ninguna)
    // Cancelled     → (ninguna)
    private static readonly IReadOnlyDictionary<WorkOrderStatus, IReadOnlySet<WorkOrderStatus>> _validTransitions =
        new Dictionary<WorkOrderStatus, IReadOnlySet<WorkOrderStatus>>
        {
            [WorkOrderStatus.Pending] = new HashSet<WorkOrderStatus> { WorkOrderStatus.InProgress, WorkOrderStatus.Cancelled },
            [WorkOrderStatus.InProgress] = new HashSet<WorkOrderStatus> { WorkOrderStatus.WaitingParts, WorkOrderStatus.Completed, WorkOrderStatus.Cancelled },
            [WorkOrderStatus.WaitingParts] = new HashSet<WorkOrderStatus> { WorkOrderStatus.InProgress, WorkOrderStatus.Cancelled },
            [WorkOrderStatus.Completed] = new HashSet<WorkOrderStatus> { WorkOrderStatus.Delivered, WorkOrderStatus.Cancelled },
            [WorkOrderStatus.Delivered] = new HashSet<WorkOrderStatus>(),
            [WorkOrderStatus.Cancelled] = new HashSet<WorkOrderStatus>(),
        };

    // Transiciones que el mecánico puede iniciar (REQ-14, doc 07 §3):
    private static readonly IReadOnlySet<WorkOrderStatus> _mechanicAllowedTargets =
        new HashSet<WorkOrderStatus>
        {
            WorkOrderStatus.InProgress,
            WorkOrderStatus.WaitingParts,
            WorkOrderStatus.Completed,
        };

    private readonly List<WorkOrderItem> _items = [];
    private readonly List<WorkOrderStatusChange> _statusChanges = [];

    private WorkOrder()
    {
    }

    public WorkOrder(
        Guid tenantId,
        Guid branchId,
        int number,
        Guid customerId,
        Guid vehicleId,
        int odometerKm,
        int? fuelLevel,
        string? reportedIssue,
        WorkOrderPriority priority,
        Guid? assignedMechanicId,
        Guid? sourceQuoteId,
        Guid? createdBy,
        DateTimeOffset createdAt)
    {
        TenantId = tenantId;
        BranchId = branchId;
        Number = number;
        CustomerId = customerId;
        VehicleId = vehicleId;
        OdometerKm = ValidateOdometer(odometerKm);
        FuelLevel = ValidateFuelLevel(fuelLevel);
        ReportedIssue = reportedIssue?.Trim();
        Priority = priority;
        AssignedMechanicId = assignedMechanicId;
        SourceQuoteId = sourceQuoteId;
        Status = WorkOrderStatus.Pending;

        var change = new WorkOrderStatusChange(tenantId, Id, null, WorkOrderStatus.Pending, null, createdBy, createdAt);
        _statusChanges.Add(change);
    }

    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public int Number { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid? AssignedMechanicId { get; private set; }
    public WorkOrderStatus Status { get; private set; }
    public WorkOrderPriority Priority { get; private set; }
    public int OdometerKm { get; private set; }
    public int? FuelLevel { get; private set; }
    public string? ReportedIssue { get; private set; }
    public string? MechanicNotes { get; private set; }
    public string? InternalNotes { get; private set; }
    public Guid? SourceQuoteId { get; private set; }
    public decimal SubtotalNet { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal Total { get; private set; }
    public decimal PaidAmount { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancelReason { get; private set; }
    public uint Version { get; private set; }

    // IAuditable
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public IReadOnlyList<WorkOrderItem> Items => _items.AsReadOnly();
    public IReadOnlyList<WorkOrderStatusChange> StatusChanges => _statusChanges.AsReadOnly();

    /// <summary>
    /// Cambia el estado de la orden validando la transición y los permisos del rol.
    /// </summary>
    /// <param name="toStatus">Estado destino.</param>
    /// <param name="isMechanic">True cuando el actor es un mecánico (transiciones acotadas).</param>
    /// <param name="canDeliver">Actor tiene workorders.deliver.</param>
    /// <param name="canCancel">Actor tiene workorders.cancel.</param>
    /// <param name="cancelReason">Obligatorio si toStatus == Cancelled.</param>
    /// <param name="note">Nota opcional.</param>
    /// <param name="changedBy">Actor que cambia el estado.</param>
    /// <param name="changedAt">Momento del cambio.</param>
    public void CambiarEstado(
        WorkOrderStatus toStatus,
        bool isMechanic,
        bool canDeliver,
        bool canCancel,
        string? cancelReason,
        string? note,
        Guid? changedBy,
        DateTimeOffset changedAt)
    {
        if (!_validTransitions[Status].Contains(toStatus))
            throw new InvalidOperationException(
                $"La transición {Status} → {toStatus} no es válida.");

        if (toStatus == WorkOrderStatus.Delivered)
        {
            if (!canDeliver)
                throw new UnauthorizedAccessException(
                    $"No tiene permiso para marcar una orden como {WorkOrderStatus.Delivered}.");
        }

        if (toStatus == WorkOrderStatus.Cancelled)
        {
            if (!canCancel)
                throw new UnauthorizedAccessException(
                    $"No tiene permiso para cancelar una orden.");

            if (string.IsNullOrWhiteSpace(cancelReason))
                throw new ArgumentException("El motivo de cancelación es obligatorio.", nameof(cancelReason));

            // D-09 provisional: no se puede cancelar si ya hay pagos aplicados.
            if (PaidAmount > 0)
                throw new InvalidOperationException(
                    "Anulá o reintegrá los pagos antes de cancelar la orden.");
        }

        if (isMechanic && !_mechanicAllowedTargets.Contains(toStatus))
            throw new UnauthorizedAccessException(
                $"El mecánico no puede realizar la transición hacia {toStatus}.");

        var prevStatus = Status;
        Status = toStatus;

        if (toStatus == WorkOrderStatus.Completed)
            CompletedAt = changedAt;
        else if (toStatus == WorkOrderStatus.Delivered)
            DeliveredAt = changedAt;
        else if (toStatus == WorkOrderStatus.Cancelled)
        {
            CancelledAt = changedAt;
            CancelReason = cancelReason!.Trim();
        }

        _statusChanges.Add(new WorkOrderStatusChange(TenantId, Id, prevStatus, toStatus, note, changedBy, changedAt));
    }

    /// <summary>
    /// Asigna un mecánico a la orden.
    /// Los mecánicos solo pueden asignarse a sí mismos en órdenes sin asignar (workorders.assign_self).
    /// </summary>
    public void Assign(Guid mechanicMembershipId, bool isMechanic, Guid actorMembershipId)
    {
        if (isMechanic)
        {
            if (AssignedMechanicId.HasValue)
                throw new InvalidOperationException("La orden ya está asignada a un mecánico.");

            if (mechanicMembershipId != actorMembershipId)
                throw new UnauthorizedAccessException("El mecánico solo puede asignarse a sí mismo.");
        }

        if (Status is WorkOrderStatus.Delivered or WorkOrderStatus.Cancelled)
            throw new InvalidOperationException("No se puede asignar un mecánico a una orden finalizada.");

        AssignedMechanicId = mechanicMembershipId;
    }

    /// <summary>Actualiza los datos de recepción (datos editables mientras no está Delivered/Cancelled).</summary>
    public void UpdateReception(
        int odometerKm,
        int? fuelLevel,
        string? reportedIssue,
        string? internalNotes,
        WorkOrderPriority priority,
        Guid? assignedMechanicId)
    {
        GuardEditable();
        OdometerKm = ValidateOdometer(odometerKm);
        FuelLevel = ValidateFuelLevel(fuelLevel);
        ReportedIssue = reportedIssue?.Trim();
        InternalNotes = internalNotes?.Trim();
        Priority = priority;
        AssignedMechanicId = assignedMechanicId;
    }

    /// <summary>Actualiza las notas del trabajo (mecánico + admin + owner).</summary>
    public void UpdateMechanicNotes(string? notes)
    {
        if (Status == WorkOrderStatus.Delivered)
            throw new InvalidOperationException("No se pueden editar las notas de una orden entregada.");
        MechanicNotes = notes?.Trim();
    }

    /// <summary>Agrega un renglón y recalcula totales.</summary>
    public WorkOrderItem AddItem(
        WorkOrderItemKind kind,
        string description,
        Guid? catalogItemId,
        decimal quantity,
        decimal unitNetPrice,
        decimal vatRate,
        decimal? unitCostSnapshot,
        Guid? quoteItemId = null)
    {
        GuardEditable();
        var position = _items.Count > 0 ? _items.Max(i => i.Position) + 1 : 1;
        var item = new WorkOrderItem(
            TenantId, Id, kind, description, catalogItemId,
            quantity, unitNetPrice, vatRate, unitCostSnapshot, position, quoteItemId);
        _items.Add(item);
        RecalculateTotals();
        return item;
    }

    /// <summary>Actualiza un renglón y recalcula totales.</summary>
    public void UpdateItem(Guid itemId, string description, decimal quantity, decimal unitNetPrice, decimal vatRate, int position)
    {
        GuardEditable();
        var item = FindItem(itemId);
        item.Update(description, quantity, unitNetPrice, vatRate, position);
        RecalculateTotals();
    }

    /// <summary>
    /// Elimina un renglón. Retorna la cantidad del ítem (para revertir stock). Solo antes de Delivered.
    /// </summary>
    public (Guid? CatalogItemId, WorkOrderItemKind Kind, decimal Quantity) RemoveItem(Guid itemId)
    {
        if (Status == WorkOrderStatus.Delivered)
            throw new InvalidOperationException("No se pueden quitar renglones de una orden entregada.");

        var item = FindItem(itemId);
        _items.Remove(item);
        RecalculateTotals();
        return (item.CatalogItemId, item.Kind, item.Quantity);
    }

    private WorkOrderItem FindItem(Guid itemId) =>
        _items.FirstOrDefault(i => i.Id == itemId)
        ?? throw new InvalidOperationException($"El renglón {itemId} no pertenece a esta orden.");

    private void GuardEditable()
    {
        if (Status is WorkOrderStatus.Delivered or WorkOrderStatus.Cancelled)
            throw new InvalidOperationException(
                $"No se puede modificar una orden en estado {Status}.");
    }

    private void RecalculateTotals()
    {
        SubtotalNet = _items.Sum(i => i.LineNet);
        VatAmount = _items.Sum(i => i.LineVat);
        DiscountAmount = 0; // Descuentos en E6 (presupuestos)
        Total = SubtotalNet - DiscountAmount + VatAmount;
    }

    private static int ValidateOdometer(int km)
    {
        if (km < 0)
            throw new ArgumentOutOfRangeException(nameof(km), "El odómetro no puede ser negativo.");
        return km;
    }

    private static int? ValidateFuelLevel(int? level)
    {
        if (level is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(level), "El nivel de combustible debe estar entre 0 y 100.");
        return level;
    }
}
