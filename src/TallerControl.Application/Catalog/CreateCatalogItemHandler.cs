using TallerControl.Application.Common;
using TallerControl.Domain.Catalog;

namespace TallerControl.Application.Catalog;

/// <summary>Caso de uso: crear ítem del catálogo (doc 05 §2.5, T-CAT-02).</summary>
public sealed class CreateCatalogItemHandler
{
    private readonly ICatalogRepository _repo;
    private readonly ITenantContext _tenantContext;
    private readonly IBranchContext _branchContext;
    private readonly IActorContext _actorContext;
    private readonly TimeProvider _clock;

    public CreateCatalogItemHandler(
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

    /// <param name="canEditCost">Sin permiso de costos, el costo del pedido se ignora.</param>
    public async Task<CatalogItemDto> HandleAsync(CreateCatalogItemRequest request, bool canEditCost, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        await ValidateSkuAsync(null, request.Sku, ct);

        CatalogItem item;
        try
        {
            item = new CatalogItem(
                tenantId,
                request.Kind,
                request.Name,
                request.Sku,
                request.NetPrice,
                request.VatRate,
                canEditCost ? request.Cost : null,
                request.TrackStock,
                request.StockMin,
                request.DurationMinutes,
                request.BufferMinutes,
                request.Description,
                request.StockMax);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await _repo.AddAsync(item, ct);

        if (request.TrackStock && request.InitialStockQty is { } initial && initial != 0)
            await RegisterInitialStockAsync(tenantId, item, initial, ct);

        await _repo.SaveChangesAsync(ct);

        return await _repo.GetFullAsync(item.Id, ct)
            ?? throw new InvalidOperationException("El ítem recién creado no se pudo leer.");
    }

    private async Task RegisterInitialStockAsync(Guid tenantId, CatalogItem item, decimal quantity, CancellationToken ct)
    {
        var branchId = _branchContext.BranchId
            ?? await _repo.GetMainBranchIdAsync(tenantId, ct)
            ?? throw new InvalidOperationException("El taller no tiene una sucursal principal configurada.");

        var movement = new StockMovement(
            tenantId, branchId, item.Id, quantity, StockMovementReason.ManualAdjust,
            "Stock inicial",
            workOrderItemId: null, createdBy: _actorContext.ActorId, createdAt: _clock.GetUtcNow());

        try
        {
            item.ApplyStockMovement(quantity);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await _repo.AddMovementAsync(movement, ct);
    }

    private async Task ValidateSkuAsync(Guid? excludeId, string? sku, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sku)) return;

        if (await _repo.SkuExistsAsync(excludeId, sku, ct))
            throw new ConflictException("Ya existe un ítem con ese SKU.");
    }
}
