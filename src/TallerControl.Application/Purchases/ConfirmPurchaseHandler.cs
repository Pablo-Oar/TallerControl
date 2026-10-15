using TallerControl.Application.Cash;
using TallerControl.Application.Catalog;
using TallerControl.Application.Common;
using TallerControl.Domain.Cash;
using TallerControl.Domain.Catalog;

namespace TallerControl.Application.Purchases;

/// <summary>
/// Confirma la compra: actualiza stock y genera movimiento de caja si se especificó una cuenta (T-PUR-01, T-PUR-02).
/// </summary>
public sealed class ConfirmPurchaseHandler
{
    private readonly IPurchaseRepository _repo;
    private readonly ICatalogRepository _catalogRepo;
    private readonly ICashRepository _cashRepo;
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly TimeProvider _clock;

    public ConfirmPurchaseHandler(
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

    public async Task<PurchaseDto> HandleAsync(Guid id, ConfirmPurchaseRequest request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        var purchase = await _repo.FindByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException("La compra no existe.");

        if (purchase.Version != request.Version)
            throw new ConcurrencyException();

        // Validar cuenta de caja si se proporcionó
        if (request.AccountId.HasValue)
        {
            _ = await _cashRepo.FindAccountByIdAsync(tenantId, request.AccountId.Value, ct)
                ?? throw new NotFoundException("La cuenta de caja no existe.");
        }

        try
        {
            purchase.Confirm(request.AccountId);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        var now = _clock.GetUtcNow();

        // Actualizar stock por cada ítem con CatalogItemId
        foreach (var item in purchase.Items.Where(i => i.CatalogItemId.HasValue))
        {
            var catalogItem = await _catalogRepo.FindActiveByIdAsync(item.CatalogItemId!.Value, ct);
            if (catalogItem is null || !catalogItem.TrackStock) continue;

            var movement = new StockMovement(
                tenantId, purchase.BranchId, item.CatalogItemId!.Value,
                item.Quantity, StockMovementReason.Purchase,
                note: null, workOrderItemId: null,
                createdBy: _actor.ActorId, createdAt: now);

            catalogItem.ApplyStockMovement(item.Quantity);
            await _catalogRepo.AddMovementAsync(movement, ct);
        }

        // Crear movimiento de caja si se vinculó una cuenta
        if (request.AccountId.HasValue)
        {
            var account = await _cashRepo.FindAccountByIdAsync(tenantId, request.AccountId.Value, ct);
            if (account is not null)
            {
                var cashMovement = new CashMovement(
                    tenantId, account.BranchId, account.Id,
                    purchase.Total, CashMovementDirection.Out,
                    CashMovementReason.PurchasePayment,
                    referenceType: "purchase", referenceId: purchase.Id,
                    note: null, createdByMembershipId: _actor.ActorId, createdAt: now);

                await _cashRepo.AddMovementAsync(cashMovement, ct);
            }
        }

        await _repo.SaveChangesAsync(ct);
        return CreatePurchaseHandler.MapToDto(purchase);
    }
}
