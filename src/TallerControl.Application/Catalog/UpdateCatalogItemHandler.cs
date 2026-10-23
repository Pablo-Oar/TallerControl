using TallerControl.Application.Common;
using TallerControl.Domain.Catalog;

namespace TallerControl.Application.Catalog;

/// <summary>Caso de uso: editar ítem del catálogo con concurrencia optimista (doc 05 §2.5, T-CAT-03..04, K-01).</summary>
public sealed class UpdateCatalogItemHandler
{
    private readonly ICatalogRepository _repo;
    private readonly ITenantContext _tenantContext;
    private readonly IBranchContext _branchContext;
    private readonly IActorContext _actorContext;
    private readonly TimeProvider _clock;

    public UpdateCatalogItemHandler(
        ICatalogRepository repo,
        ITenantContext tenantContext,
        IBranchContext branchContext,
        IActorContext actorContext,
        TimeProvider clock)
    {
        _repo = repo;
        _tenantContext = tenantContext;
        _branchContext = branchContext;
        _actorContext = actorContext;
        _clock = clock;
    }

    /// <param name="canEditCost">Sin permiso de costos, el costo del pedido se ignora y se conserva el actual.</param>
    public async Task<CatalogItemDto> HandleAsync(
        Guid id, uint ifMatchVersion, UpdateCatalogItemRequest request, bool canEditCost, CancellationToken ct)
    {
        var item = await _repo.FindActiveByIdAsync(id, ct)
            ?? throw new NotFoundException();

        if (item.Version != ifMatchVersion)
            throw new ConcurrencyException();

        await ValidateSkuAsync(id, request.Sku, ct);

        try
        {
            item.Update(
                request.Name,
                request.Sku,
                request.NetPrice,
                request.VatRate,
                canEditCost ? request.Cost : item.Cost,
                request.StockMin,
                request.DurationMinutes,
                request.BufferMinutes,
                request.IsActive,
                request.Description,
                request.StockMax);

            // Activar el control de stock con una cantidad inicial: queda registrada como un movimiento.
            if (request.TrackStock is { } wantsTracking && wantsTracking != item.TrackStock)
            {
                item.SetTrackStock(wantsTracking);
                if (wantsTracking && request.InitialStockQty is { } initial && initial != 0)
                    await RegisterInitialStockAsync(item, initial, ct);
            }
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);

        return await _repo.GetFullAsync(id, ct)
            ?? throw new InvalidOperationException("El ítem no se pudo leer después de la edición.");
    }

    private async Task RegisterInitialStockAsync(CatalogItem item, decimal quantity, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");
        var branchId = _branchContext.BranchId
            ?? await _repo.GetMainBranchIdAsync(tenantId, ct)
            ?? throw new InvalidOperationException("El taller no tiene una sucursal principal configurada.");

        var movement = new StockMovement(
            tenantId, branchId, item.Id, quantity, StockMovementReason.ManualAdjust,
            "Stock inicial al activar el control de stock",
            workOrderItemId: null, createdBy: _actorContext.ActorId, createdAt: _clock.GetUtcNow());

        item.ApplyStockMovement(quantity);
        await _repo.AddMovementAsync(movement, ct);
    }

    private async Task ValidateSkuAsync(Guid excludeId, string? sku, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sku)) return;

        if (await _repo.SkuExistsAsync(excludeId, sku, ct))
            throw new ConflictException("Ya existe un ítem con ese SKU.");
    }
}
