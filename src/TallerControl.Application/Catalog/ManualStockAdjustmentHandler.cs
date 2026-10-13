using TallerControl.Application.Common;
using TallerControl.Domain.Catalog;

namespace TallerControl.Application.Catalog;

/// <summary>
/// Caso de uso: ajuste manual de stock (doc 05 §2.5, T-STK-01).
/// Crea un <see cref="StockMovement"/> y actualiza <see cref="CatalogItem.StockQty"/> en la misma transacción.
/// D-07: stock negativo permitido (se registra con nota).
/// </summary>
public sealed class ManualStockAdjustmentHandler
{
    private readonly ICatalogRepository _repo;
    private readonly ITenantContext _tenantContext;
    private readonly IBranchContext _branchContext;
    private readonly IActorContext _actorContext;
    private readonly TimeProvider _clock;

    public ManualStockAdjustmentHandler(
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

    public async Task<CatalogItemDto> HandleAsync(
        Guid catalogItemId, ManualStockAdjustmentRequest request, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        // Verificar primero la existencia del ítem (filtro de taller) → 404 si no pertenece al taller.
        // Luego resolver la sucursal para no exponer 500 cuando el ítem es de otro taller.
        var item = await _repo.FindActiveByIdAsync(catalogItemId, ct)
            ?? throw new NotFoundException();

        var branchId = _branchContext.BranchId
            ?? await _repo.GetMainBranchIdAsync(tenantId, ct)
            ?? throw new InvalidOperationException("El taller no tiene una sucursal principal configurada.");

        if (request.Quantity == 0)
            throw new BusinessRuleException("La cantidad del ajuste no puede ser cero.");

        if (string.IsNullOrWhiteSpace(request.Note))
            throw new BusinessRuleException("La nota es obligatoria para ajustes manuales.");

        StockMovement movement;
        try
        {
            movement = new StockMovement(
                tenantId,
                branchId,
                catalogItemId,
                request.Quantity,
                StockMovementReason.ManualAdjust,
                request.Note,
                workOrderItemId: null,
                createdBy: _actorContext.ActorId,
                createdAt: _clock.GetUtcNow());

            item.ApplyStockMovement(request.Quantity);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await _repo.AddMovementAsync(movement, ct);
        await _repo.SaveChangesAsync(ct);

        return await _repo.GetFullAsync(catalogItemId, ct)
            ?? throw new InvalidOperationException("El ítem no se pudo leer después del ajuste.");
    }
}
