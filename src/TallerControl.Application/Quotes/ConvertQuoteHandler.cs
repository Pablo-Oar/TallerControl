using TallerControl.Application.Catalog;
using TallerControl.Application.Common;
using TallerControl.Application.Tenancy;
using TallerControl.Application.Vehicles;
using TallerControl.Application.Work;
using TallerControl.Domain.Catalog;
using TallerControl.Domain.Quotes;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Work;

namespace TallerControl.Application.Quotes;

/// <summary>
/// Caso de uso: convertir un presupuesto aprobado en una orden de trabajo (doc 05 §2.6, REQ-11, T-QUO-09).
/// Soporta idempotencia: si el presupuesto ya está convertido devuelve la OT existente.
/// </summary>
public sealed class ConvertQuoteHandler
{
    private readonly IQuoteRepository _quoteRepo;
    private readonly IWorkOrderRepository _woRepo;
    private readonly ICatalogRepository _catalogRepo;
    private readonly IVehicleRepository _vehicleRepo;
    private readonly ITenantContext _tenantContext;
    private readonly IBranchContext _branchContext;
    private readonly IActorContext _actorContext;
    private readonly IDocumentNumberGenerator _numbers;
    private readonly TimeProvider _clock;

    public ConvertQuoteHandler(
        IQuoteRepository quoteRepo,
        IWorkOrderRepository woRepo,
        ICatalogRepository catalogRepo,
        IVehicleRepository vehicleRepo,
        ITenantContext tenantContext,
        IBranchContext branchContext,
        IActorContext actorContext,
        IDocumentNumberGenerator numbers,
        TimeProvider clock)
    {
        _quoteRepo = quoteRepo;
        _woRepo = woRepo;
        _catalogRepo = catalogRepo;
        _vehicleRepo = vehicleRepo;
        _tenantContext = tenantContext;
        _branchContext = branchContext;
        _actorContext = actorContext;
        _numbers = numbers;
        _clock = clock;
    }

    public async Task<WorkOrderDto> HandleAsync(Guid quoteId, ConvertQuoteRequest request, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        var quote = await _quoteRepo.FindByIdAsync(quoteId, ct)
            ?? throw new NotFoundException("El presupuesto no existe.");

        // Idempotencia: si ya está convertido, devuelve la OT.
        if (quote.Status == QuoteStatus.Converted && quote.WorkOrderId.HasValue)
        {
            return await _woRepo.GetFullAsync(quote.WorkOrderId.Value, includePrices: true, ct)
                ?? throw new InvalidOperationException("La orden vinculada al presupuesto no se pudo leer.");
        }

        if (quote.Status is not (QuoteStatus.Approved or QuoteStatus.PartiallyApproved))
            throw new BusinessRuleException(
                $"Solo se pueden convertir presupuestos aprobados (estado actual: {quote.Status}).");

        var branchId = _branchContext.BranchId
            ?? await _woRepo.GetMainBranchIdAsync(tenantId, ct)
            ?? throw new InvalidOperationException("El taller no tiene una sucursal principal configurada.");

        var now = _clock.GetUtcNow();
        var actorId = _actorContext.ActorId;

        // Determinar ítems aprobados (todos si Approved, solo los de RequiredForApproval=false en PartiallyApproved).
        var currentResponse = quote.Responses.FirstOrDefault(r => r.IsCurrent);
        var approvedItemIds = currentResponse != null
            ? currentResponse.Items.Where(ri => ri.Approved).Select(ri => ri.QuoteItemId).ToHashSet()
            : quote.Items.Select(i => i.Id).ToHashSet();

        var itemsToConvert = quote.Items.Where(i => approvedItemIds.Contains(i.Id)).ToList();

        await using var tx = await _woRepo.BeginTransactionAsync(ct);
        var number = await _numbers.NextAsync(DocumentKind.WorkOrder, ct);

        var order = new WorkOrder(
            tenantId, branchId, number,
            quote.CustomerId, quote.VehicleId,
            request.OdometerKm, request.FuelLevel,
            request.ReportedIssue,
            WorkOrderPriority.Normal,
            assignedMechanicId: null,
            sourceQuoteId: quote.Id,
            createdBy: actorId,
            createdAt: now);

        var stockMovements = new List<(StockMovement Movement, CatalogItem Item)>();

        foreach (var quoteItem in itemsToConvert)
        {
            CatalogItem? catalogItem = null;
            if (quoteItem.CatalogItemId.HasValue)
            {
                catalogItem = await _catalogRepo.FindActiveByIdAsync(quoteItem.CatalogItemId.Value, ct);
                // Si el ítem ya no existe en catálogo, se agrega igual sin descuento de stock.
            }

            var woItem = order.AddItem(
                quoteItem.Kind switch
                {
                    QuoteItemKind.Service => WorkOrderItemKind.Service,
                    QuoteItemKind.Product => WorkOrderItemKind.Product,
                    _ => WorkOrderItemKind.Manual,
                },
                quoteItem.Description,
                quoteItem.CatalogItemId,
                quoteItem.Quantity,
                quoteItem.UnitNetPrice,
                quoteItem.VatRate,
                catalogItem?.Cost,
                quoteItemId: quoteItem.Id);

            if (catalogItem != null && catalogItem.TrackStock)
            {
                var movement = new StockMovement(
                    tenantId, branchId, catalogItem.Id,
                    -woItem.Quantity,
                    StockMovementReason.WorkOrder,
                    note: null,
                    workOrderItemId: woItem.Id,
                    createdBy: actorId,
                    createdAt: now);

                catalogItem.ApplyStockMovement(-woItem.Quantity);
                stockMovements.Add((movement, catalogItem));
            }
        }

        // Aplicar el descuento global del presupuesto a la OT (E6).
        if (quote.GlobalDiscountPct != 0)
            order.SetGlobalDiscountFromQuote(quote.GlobalDiscountPct);

        quote.MarkConverted(order.Id, now);

        // Como al crear una orden: el kilometraje del vehículo sube si el de ingreso es mayor (T-WO-03).
        var vehicle = await _vehicleRepo.FindByIdAsync(quote.VehicleId, ct);
        if (vehicle is not null && request.OdometerKm > vehicle.OdometerKm)
            vehicle.UpdateOdometer(request.OdometerKm, allowDecrease: false);

        await _woRepo.AddAsync(order, ct);
        foreach (var (movement, _) in stockMovements)
            await _catalogRepo.AddMovementAsync(movement, ct);

        await _quoteRepo.SaveChangesAsync(ct);
        await _woRepo.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await _woRepo.GetFullAsync(order.Id, includePrices: true, ct)
            ?? throw new InvalidOperationException("La orden recién creada no se pudo leer.");
    }
}
