using TallerControl.Application.Catalog;
using TallerControl.Application.Common;
using TallerControl.Domain.Catalog;

namespace TallerControl.Application.CounterSales;

public sealed class CancelCounterSaleHandler
{
    private readonly ICounterSaleRepository _repo;
    private readonly ICatalogRepository _catalogRepo;
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly TimeProvider _clock;

    public CancelCounterSaleHandler(
        ICounterSaleRepository repo,
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

    public async Task<CounterSaleDto> HandleAsync(Guid id, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        var sale = await _repo.FindByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException("La venta no existe.");

        var wasConfirmed = sale.Status == Domain.CounterSales.CounterSaleStatus.Confirmed;

        try
        {
            sale.Cancel();
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        // Si estaba confirmada, revertir stock
        if (wasConfirmed)
        {
            var now = _clock.GetUtcNow();
            foreach (var item in sale.Items.Where(i => i.CatalogItemId.HasValue))
            {
                var catalogItem = await _catalogRepo.FindActiveByIdAsync(item.CatalogItemId!.Value, ct);
                if (catalogItem is null || !catalogItem.TrackStock) continue;

                var movement = new StockMovement(
                    tenantId, sale.BranchId, item.CatalogItemId!.Value,
                    item.Quantity, StockMovementReason.CounterSaleCancel,
                    note: null, workOrderItemId: null,
                    createdBy: _actor.ActorId, createdAt: now);

                catalogItem.ApplyStockMovement(item.Quantity);
                await _catalogRepo.AddMovementAsync(movement, ct);
            }
        }

        await _repo.SaveChangesAsync(ct);
        return CreateCounterSaleHandler.MapToDto(sale);
    }
}
