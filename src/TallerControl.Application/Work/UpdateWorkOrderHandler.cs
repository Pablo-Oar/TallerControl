using TallerControl.Application.Catalog;
using TallerControl.Application.Common;
using TallerControl.Application.Vehicles;
using TallerControl.Domain.Catalog;
using TallerControl.Domain.Work;

namespace TallerControl.Application.Work;

/// <summary>
/// Caso de uso: editar cabecera e ítems de una OT (doc 05 §2.7, T-WO-04, REQ-12).
/// Concurrencia optimista con xmin/If-Match.
/// Reemplaza la lista de ítems completa; ajusta stock por diferencia usando StockMovementReason.ItemRemoved para quitas.
/// </summary>
public sealed class UpdateWorkOrderHandler
{
    private readonly IWorkOrderRepository _woRepo;
    private readonly ICatalogRepository _catalogRepo;
    private readonly IVehicleRepository _vehicleRepo;
    private readonly IActorContext _actorContext;
    private readonly TimeProvider _clock;

    public UpdateWorkOrderHandler(
        IWorkOrderRepository woRepo,
        ICatalogRepository catalogRepo,
        IVehicleRepository vehicleRepo,
        IActorContext actorContext,
        TimeProvider clock)
    {
        _woRepo = woRepo;
        _catalogRepo = catalogRepo;
        _vehicleRepo = vehicleRepo;
        _actorContext = actorContext;
        _clock = clock;
    }

    public async Task<WorkOrderDto> HandleAsync(Guid id, UpdateWorkOrderRequest request, uint version, CancellationToken ct)
    {
        var order = await _woRepo.FindByIdAsync(id, ct)
            ?? throw new NotFoundException("La orden no existe.");

        if (order.Version != version)
            throw new ConcurrencyException();

        var now = _clock.GetUtcNow();
        var actorId = _actorContext.ActorId;

        try
        {
            order.UpdateReception(
                request.OdometerKm,
                request.FuelLevel,
                request.ReportedIssue,
                request.InternalNotes,
                request.Priority,
                request.AssignedMechanicId);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        // Actualizar km del vehículo si el nuevo es mayor (T-WO-03).
        var vehicle = await _vehicleRepo.FindByIdAsync(order.VehicleId, ct);
        if (vehicle != null && request.OdometerKm > vehicle.OdometerKm)
            vehicle.UpdateOdometer(request.OdometerKm, allowDecrease: false);

        // Reemplazar ítems: quitar los que no vienen en el request y revertir stock.
        var existingItems = order.Items.ToList();
        var stockMovements = new List<(StockMovement Movement, CatalogItem Item)>();

        foreach (var item in existingItems)
        {
            var (catalogItemId, kind, qty) = order.RemoveItem(item.Id);
            _woRepo.DeleteItem(item);

            if (catalogItemId.HasValue && kind == WorkOrderItemKind.Product)
            {
                var catalogItem = await _catalogRepo.FindActiveByIdAsync(catalogItemId.Value, ct);
                if (catalogItem != null && catalogItem.TrackStock)
                {
                    var movement = new StockMovement(
                        order.TenantId, order.BranchId, catalogItem.Id,
                        qty, StockMovementReason.ItemRemoved,
                        note: null, workOrderItemId: item.Id,
                        createdBy: actorId, createdAt: now);

                    catalogItem.ApplyStockMovement(qty);
                    stockMovements.Add((movement, catalogItem));
                }
            }
        }

        // Agregar nuevos ítems y descontar stock.
        foreach (var itemReq in request.Items)
        {
            CatalogItem? catalogItem = null;
            if (itemReq.CatalogItemId.HasValue)
            {
                catalogItem = await _catalogRepo.FindActiveByIdAsync(itemReq.CatalogItemId.Value, ct)
                    ?? throw new NotFoundException($"El ítem del catálogo {itemReq.CatalogItemId} no existe.");
            }

            var workItem = order.AddItem(
                itemReq.Kind,
                itemReq.Description,
                itemReq.CatalogItemId,
                itemReq.Quantity,
                itemReq.UnitNetPrice,
                itemReq.VatRate,
                catalogItem?.Cost,
                quoteItemId: null);

            if (catalogItem != null && catalogItem.TrackStock)
            {
                var movement = new StockMovement(
                    order.TenantId, order.BranchId, catalogItem.Id,
                    -workItem.Quantity, StockMovementReason.WorkOrder,
                    note: null, workOrderItemId: workItem.Id,
                    createdBy: actorId, createdAt: now);

                catalogItem.ApplyStockMovement(-workItem.Quantity);
                stockMovements.Add((movement, catalogItem));
            }
        }

        // El total nunca puede quedar por debajo de lo ya cobrado: dejaría un saldo negativo.
        if (order.Total < order.PaidAmount)
            throw new BusinessRuleException(
                "El total de la orden no puede quedar por debajo de lo ya cobrado. Anulá el cobro o dejá un total mayor.");

        foreach (var (movement, _) in stockMovements)
            await _catalogRepo.AddMovementAsync(movement, ct);

        await _woRepo.SaveChangesAsync(ct);

        return await _woRepo.GetFullAsync(id, includePrices: true, ct)
            ?? throw new InvalidOperationException("La orden actualizada no se pudo leer.");
    }
}
