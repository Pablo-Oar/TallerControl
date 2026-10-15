using TallerControl.Application.Catalog;
using TallerControl.Application.Common;
using TallerControl.Domain.Catalog;

namespace TallerControl.Application.Purchases;

/// <summary>
/// Cancela una compra Draft. Si ya estaba Confirmed lanza BusinessRuleException (usar nota de crédito en E13).
/// </summary>
public sealed class CancelPurchaseHandler
{
    private readonly IPurchaseRepository _repo;
    private readonly ICatalogRepository _catalogRepo;
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly TimeProvider _clock;

    public CancelPurchaseHandler(
        IPurchaseRepository repo,
        ICatalogRepository catalogRepo,
        ITenantContext tenant,
        IActorContext actor,
        TimeProvider clock)
    {
        _repo = repo;
        _catalogRepo = catalogRepo;
        _tenant = tenant;
        _actor = actor;
        _clock = clock;
    }

    public async Task<PurchaseDto> HandleAsync(Guid id, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        var purchase = await _repo.FindByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException("La compra no existe.");

        var wasConfirmed = purchase.Status == Domain.Purchases.PurchaseStatus.Confirmed;

        try
        {
            purchase.Cancel();
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        // Si estaba confirmada, revertir stock
        if (wasConfirmed)
        {
            var now = _clock.GetUtcNow();
            foreach (var item in purchase.Items.Where(i => i.CatalogItemId.HasValue))
            {
                var catalogItem = await _catalogRepo.FindActiveByIdAsync(item.CatalogItemId!.Value, ct);
                if (catalogItem is null || !catalogItem.TrackStock) continue;

                var movement = new StockMovement(
                    tenantId, purchase.BranchId, item.CatalogItemId!.Value,
                    -item.Quantity, StockMovementReason.PurchaseCancel,
                    note: null, workOrderItemId: null,
                    createdBy: _actor.ActorId, createdAt: now);

                catalogItem.ApplyStockMovement(-item.Quantity);
                await _catalogRepo.AddMovementAsync(movement, ct);
            }
        }

        await _repo.SaveChangesAsync(ct);
        return CreatePurchaseHandler.MapToDto(purchase);
    }
}
