using TallerControl.Application.Catalog;
using TallerControl.Application.Common;
using TallerControl.Application.Tenancy;
using TallerControl.Application.Users;
using TallerControl.Application.Vehicles;
using TallerControl.Domain.Catalog;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Work;

namespace TallerControl.Application.Work;

/// <summary>
/// Caso de uso: crear orden directa (doc 05 §2.7, T-WO-02, T-WO-03, T-WO-04, REQ-12).
/// Descuenta stock de los productos e intenta actualizar el km del vehículo.
/// </summary>
public sealed class CreateWorkOrderHandler
{
    private readonly IWorkOrderRepository _woRepo;
    private readonly ICatalogRepository _catalogRepo;
    private readonly IVehicleRepository _vehicleRepo;
    private readonly ITenantContext _tenantContext;
    private readonly IBranchContext _branchContext;
    private readonly IActorContext _actorContext;
    private readonly IDocumentNumberGenerator _numbers;
    private readonly TimeProvider _clock;
    private readonly IOutbox _outbox;
    private readonly IMembershipRepository _memberships;

    public CreateWorkOrderHandler(
        IWorkOrderRepository woRepo,
        ICatalogRepository catalogRepo,
        IVehicleRepository vehicleRepo,
        ITenantContext tenantContext,
        IBranchContext branchContext,
        IActorContext actorContext,
        IDocumentNumberGenerator numbers,
        TimeProvider clock,
        IOutbox outbox,
        IMembershipRepository memberships)
    {
        _woRepo = woRepo;
        _catalogRepo = catalogRepo;
        _vehicleRepo = vehicleRepo;
        _tenantContext = tenantContext;
        _branchContext = branchContext;
        _actorContext = actorContext;
        _numbers = numbers;
        _clock = clock;
        _outbox = outbox;
        _memberships = memberships;
    }

    public async Task<WorkOrderDto> HandleAsync(CreateWorkOrderRequest request, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        await MechanicAssignment.EnsureAssignableAsync(_memberships, request.AssignedMechanicId, ct);

        // Validar que el vehículo exista y pertenezca al cliente del mismo taller.
        var vehicle = await _vehicleRepo.FindByIdAsync(request.VehicleId, ct)
            ?? throw new NotFoundException("El vehículo no existe.");

        if (vehicle.CustomerId != request.CustomerId)
            throw new BusinessRuleException("El vehículo no pertenece al cliente indicado.");

        var branchId = _branchContext.BranchId
            ?? await _woRepo.GetMainBranchIdAsync(tenantId, ct)
            ?? throw new InvalidOperationException("El taller no tiene una sucursal principal configurada.");

        var now = _clock.GetUtcNow();
        var actorId = _actorContext.ActorId;

        // La numeración exige una transacción activa (SELECT FOR UPDATE sin rollback de número).
        await using var tx = await _woRepo.BeginTransactionAsync(ct);

        var number = await _numbers.NextAsync(DocumentKind.WorkOrder, ct);

        WorkOrder order;
        try
        {
            order = new WorkOrder(
                tenantId, branchId, number,
                request.CustomerId, request.VehicleId,
                request.OdometerKm, request.FuelLevel,
                request.ReportedIssue, request.Priority,
                request.AssignedMechanicId,
                sourceQuoteId: null,
                createdBy: actorId,
                createdAt: now);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        // Agregar ítems y descontar stock.
        var stockMovements = new List<(StockMovement Movement, CatalogItem Item)>();
        if (request.Items != null)
        {
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

                // Descontar stock de productos con seguimiento (T-WO-02).
                if (catalogItem != null && catalogItem.TrackStock)
                {
                    var movement = new StockMovement(
                        tenantId, branchId, catalogItem.Id,
                        -workItem.Quantity,
                        StockMovementReason.WorkOrder,
                        note: null,
                        workOrderItemId: workItem.Id,
                        createdBy: actorId,
                        createdAt: now);

                    catalogItem.ApplyStockMovement(-workItem.Quantity);
                    stockMovements.Add((movement, catalogItem));
                }
            }
        }

        // Actualizar km del vehículo si el nuevo es mayor (T-WO-03).
        if (request.OdometerKm > vehicle.OdometerKm)
        {
            vehicle.UpdateOdometer(request.OdometerKm, allowDecrease: false);
        }

        await _woRepo.AddAsync(order, ct);
        foreach (var (movement, _) in stockMovements)
            await _catalogRepo.AddMovementAsync(movement, ct);

        // Si la orden nace con mecánico asignado, él también recibe el aviso en el celular.
        if (request.AssignedMechanicId is { } assignedMechanic)
        {
            _outbox.Enqueue("push.work_order_assigned", new
            {
                membershipId = assignedMechanic,
                orderNumber = order.Number,
                workOrderId = order.Id,
            });
        }

        await _woRepo.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await _woRepo.GetFullAsync(order.Id, includePrices: true, ct)
            ?? throw new InvalidOperationException("La orden recién creada no se pudo leer.");
    }
}
