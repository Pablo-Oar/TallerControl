using TallerControl.Application.Cash;
using TallerControl.Application.Catalog;
using TallerControl.Application.Common;
using TallerControl.Domain.Cash;
using TallerControl.Domain.Catalog;

namespace TallerControl.Application.CounterSales;

/// <summary>
/// Confirma la venta de mostrador: descuenta stock y genera movimiento de caja si corresponde (E11-C, T-CSAL-01..02).
/// </summary>
public sealed class ConfirmCounterSaleHandler
{
    private readonly ICounterSaleRepository _repo;
    private readonly ICatalogRepository _catalogRepo;
    private readonly ICashRepository _cashRepo;
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly TimeProvider _clock;

    public ConfirmCounterSaleHandler(
        ICounterSaleRepository repo,
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

    public async Task<CounterSaleDto> HandleAsync(Guid id, ConfirmCounterSaleRequest request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        var sale = await _repo.FindByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException("La venta no existe.");

        if (sale.Version != request.Version)
            throw new ConcurrencyException();

        if (request.AccountId.HasValue)
        {
            _ = await _cashRepo.FindAccountByIdAsync(tenantId, request.AccountId.Value, ct)
                ?? throw new NotFoundException("La cuenta de caja no existe.");
        }

        try
        {
            sale.Confirm(request.AccountId);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        var now = _clock.GetUtcNow();

        // Descontar stock de ítems con CatalogItemId
        foreach (var item in sale.Items.Where(i => i.CatalogItemId.HasValue))
        {
            var catalogItem = await _catalogRepo.FindActiveByIdAsync(item.CatalogItemId!.Value, ct);
            if (catalogItem is null || !catalogItem.TrackStock) continue;

            var movement = new StockMovement(
                tenantId, sale.BranchId, item.CatalogItemId!.Value,
                -item.Quantity, StockMovementReason.CounterSale,
                note: null, workOrderItemId: null,
                createdBy: _actor.ActorId, createdAt: now);

            catalogItem.ApplyStockMovement(-item.Quantity);
            await _catalogRepo.AddMovementAsync(movement, ct);
        }

        // Movimiento de caja: ingreso por venta de mostrador
        if (request.AccountId.HasValue)
        {
            var account = await _cashRepo.FindAccountByIdAsync(tenantId, request.AccountId.Value, ct);
            if (account is not null)
            {
                var cashMovement = new CashMovement(
                    tenantId, account.BranchId, account.Id,
                    sale.Total, CashMovementDirection.In,
                    CashMovementReason.CounterSaleIncome,
                    referenceType: "counter_sale", referenceId: sale.Id,
                    note: null, createdByMembershipId: _actor.ActorId, createdAt: now);

                await _cashRepo.AddMovementAsync(cashMovement, ct);
            }
        }

        await _repo.SaveChangesAsync(ct);
        return CreateCounterSaleHandler.MapToDto(sale);
    }
}
