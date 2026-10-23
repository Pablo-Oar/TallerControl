using TallerControl.Application.Cash;
using TallerControl.Application.Catalog;
using TallerControl.Application.Common;
using TallerControl.Domain.Cash;
using TallerControl.Domain.Catalog;
using TallerControl.Domain.Purchases;

namespace TallerControl.Application.Purchases;

/// <summary>
/// Anula una compra. Un borrador se descarta; una confirmada exige motivo y deja contraasientos: el stock que había
/// sumado se descuenta y, si se pagó desde una cuenta de caja, el pago vuelve como ingreso (D-31: la caja es de solo
/// inserción, no se borra nada).
/// </summary>
public sealed class CancelPurchaseHandler
{
    private readonly IPurchaseRepository _repo;
    private readonly ICatalogRepository _catalogRepo;
    private readonly ICashRepository _cashRepo;
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly TimeProvider _clock;

    public CancelPurchaseHandler(
        IPurchaseRepository repo,
        ICatalogRepository catalogRepo,
        ICashRepository cashRepo,
        ITenantContext tenant,
        IActorContext actor,
        TimeProvider clock)
    {
        _repo = repo;
        _catalogRepo = catalogRepo;
        _cashRepo = cashRepo;
        _tenant = tenant;
        _actor = actor;
        _clock = clock;
    }

    public async Task<PurchaseDto> HandleAsync(Guid id, string? reason, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        var purchase = await _repo.FindByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException("La compra no existe.");

        var wasConfirmed = purchase.Status == PurchaseStatus.Confirmed;
        var now = _clock.GetUtcNow();

        try
        {
            purchase.Cancel(reason, now);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        if (wasConfirmed)
        {
            // Contraasiento de stock.
            foreach (var item in purchase.Items.Where(i => i.CatalogItemId.HasValue))
            {
                var catalogItem = await _catalogRepo.FindAnyByIdAsync(item.CatalogItemId!.Value, ct);
                if (catalogItem is null || !catalogItem.TrackStock) continue;

                var movement = new StockMovement(
                    tenantId, purchase.BranchId, catalogItem.Id,
                    -item.Quantity, StockMovementReason.PurchaseCancel,
                    note: purchase.CancelReason, workOrderItemId: null,
                    createdBy: _actor.ActorId, createdAt: now);

                catalogItem.ApplyStockMovement(-item.Quantity);
                await _catalogRepo.AddMovementAsync(movement, ct);
            }

            // Contraasiento de caja: el pago vuelve a la misma cuenta.
            if (purchase.AccountId is { } accountId)
            {
                var account = await _cashRepo.FindAccountByIdAsync(tenantId, accountId, ct);
                if (account is not null)
                {
                    await _cashRepo.AddMovementAsync(new CashMovement(
                        tenantId, account.BranchId, account.Id,
                        purchase.Total, CashMovementDirection.In,
                        CashMovementReason.ManualIncome,
                        referenceType: "purchase_cancel", referenceId: purchase.Id,
                        note: $"Anulación de la compra N° {purchase.DocumentNumber}: {purchase.CancelReason}",
                        createdByMembershipId: _actor.ActorId, createdAt: now), ct);
                }
            }
        }

        await _repo.SaveChangesAsync(ct);
        return CreatePurchaseHandler.MapToDto(purchase);
    }
}
