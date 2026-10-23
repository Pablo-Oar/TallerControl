using TallerControl.Application.Catalog;
using TallerControl.Application.Common;
using TallerControl.Domain.Catalog;
using TallerControl.Domain.Work;

namespace TallerControl.Application.Work;

/// <summary>
/// Caso de uso: cambiar el estado de una OT (doc 05 §2.7, T-WO-06, T-WO-07, T-WO-08, T-WO-09, T-WO-10, REQ-12).
/// Delega la lógica de transición a <see cref="WorkOrder.CambiarEstado"/>.
/// </summary>
public sealed class ChangeWorkOrderStatusHandler
{
    private readonly IWorkOrderRepository _repo;
    private readonly ICatalogRepository _catalogRepo;
    private readonly IActorContext _actorContext;
    private readonly TimeProvider _clock;
    private readonly IOutbox _outbox;

    public ChangeWorkOrderStatusHandler(
        IWorkOrderRepository repo,
        ICatalogRepository catalogRepo,
        IActorContext actorContext,
        TimeProvider clock,
        IOutbox outbox)
    {
        _outbox = outbox;
        _repo = repo;
        _catalogRepo = catalogRepo;
        _actorContext = actorContext;
        _clock = clock;
    }

    public async Task<WorkOrderDto> HandleAsync(
        Guid id,
        ChangeStatusRequest request,
        bool isMechanic,
        bool canDeliver,
        bool canCancel,
        CancellationToken ct)
    {
        var order = await _repo.FindByIdAsync(id, ct)
            ?? throw new NotFoundException("La orden no existe.");

        var actorId = _actorContext.ActorId;
        var now = _clock.GetUtcNow();

        try
        {
            order.CambiarEstado(
                request.ToStatus,
                isMechanic: isMechanic,
                canDeliver: canDeliver,
                canCancel: canCancel,
                cancelReason: request.CancelReason,
                note: request.Note,
                changedBy: actorId,
                changedAt: now);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        // REQ-12 CA2 / T-WO-10: cancelar repone el stock que la orden había descontado. Se hace en el mismo
        // SaveChanges que el cambio de estado: o se cancela y se repone, o no ocurre nada.
        if (request.ToStatus == WorkOrderStatus.Cancelled)
            await RestockAsync(order, actorId, now, ct);

        NotifyStatusChange(order, request.ToStatus, isMechanic);

        await _repo.SaveChangesAsync(ct);

        return await _repo.GetFullAsync(id, includePrices: true, ct)
            ?? throw new InvalidOperationException("La orden actualizada no se pudo leer.");
    }

    /// <summary>
    /// Avisos del cambio de estado, por la cola de salida (misma transacción que el cambio):
    /// si lo cambia otra persona, el mecánico asignado recibe un aviso en el celular; si lo cambia el mecánico a
    /// «Completada» o «Esperando repuestos», aparece un aviso en la campanita del panel.
    /// </summary>
    private void NotifyStatusChange(WorkOrder order, WorkOrderStatus status, bool isMechanic)
    {
        var label = StatusLabel(status);

        if (!isMechanic && order.AssignedMechanicId is { } mechanicId)
        {
            _outbox.Enqueue("push.work_order_status", new
            {
                membershipId = mechanicId,
                orderNumber = order.Number,
                workOrderId = order.Id,
                statusLabel = label,
            });
        }

        if (isMechanic && status is WorkOrderStatus.Completed or WorkOrderStatus.WaitingParts)
        {
            _outbox.Enqueue("notification.work_order_status", new
            {
                orderNumber = order.Number,
                workOrderId = order.Id,
                statusLabel = label,
            });
        }
    }

    private static string StatusLabel(WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.Pending => "Pendiente",
        WorkOrderStatus.InProgress => "En proceso",
        WorkOrderStatus.WaitingParts => "Esperando repuestos",
        WorkOrderStatus.Completed => "Completada",
        WorkOrderStatus.Delivered => "Entregada",
        WorkOrderStatus.Cancelled => "Anulada",
        _ => status.ToString(),
    };

    private async Task RestockAsync(WorkOrder order, Guid? actorId, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var item in order.Items)
        {
            if (!item.CatalogItemId.HasValue) continue;

            // Puede haberse borrado lógicamente después de vender: el stock se repone igual.
            var catalogItem = await _catalogRepo.FindAnyByIdAsync(item.CatalogItemId.Value, ct);
            if (catalogItem is null || !catalogItem.TrackStock) continue;

            var movement = new StockMovement(
                order.TenantId, order.BranchId, catalogItem.Id,
                item.Quantity, StockMovementReason.WorkOrderCancel,
                note: null, workOrderItemId: item.Id,
                createdBy: actorId, createdAt: now);

            catalogItem.ApplyStockMovement(item.Quantity);
            await _catalogRepo.AddMovementAsync(movement, ct);
        }
    }
}
