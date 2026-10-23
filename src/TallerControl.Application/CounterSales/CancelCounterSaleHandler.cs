using TallerControl.Application.Cash;
using TallerControl.Application.Catalog;
using TallerControl.Application.Common;
using TallerControl.Application.Invoicing;
using TallerControl.Domain.Cash;
using TallerControl.Domain.Catalog;
using TallerControl.Domain.CounterSales;
using TallerControl.Domain.Invoicing;

namespace TallerControl.Application.CounterSales;

/// <summary>
/// Cancela un borrador o devuelve una venta confirmada (K-04). La devolución exige motivo y deja contraasientos:
/// repone el stock, saca de la cuenta de Caja lo que había entrado y, si la venta estaba facturada, emite la nota de
/// crédito en ARCA (primero: si ARCA la rechaza, la devolución no se hace).
/// </summary>
public sealed class CancelCounterSaleHandler
{
    private readonly ICounterSaleRepository _repo;
    private readonly ICatalogRepository _catalogRepo;
    private readonly ICashRepository _cashRepo;
    private readonly IInvoicingRepository _invoicingRepo;
    private readonly VoidInvoiceHandler _voidInvoice;
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly TimeProvider _clock;

    public CancelCounterSaleHandler(
        ICounterSaleRepository repo,
        ICatalogRepository catalogRepo,
        ICashRepository cashRepo,
        IInvoicingRepository invoicingRepo,
        VoidInvoiceHandler voidInvoice,
        ITenantContext tenant,
        IActorContext actor,
        TimeProvider clock)
    {
        _repo = repo;
        _catalogRepo = catalogRepo;
        _cashRepo = cashRepo;
        _invoicingRepo = invoicingRepo;
        _voidInvoice = voidInvoice;
        _tenant = tenant;
        _actor = actor;
        _clock = clock;
    }

    public async Task<CounterSaleDto> HandleAsync(Guid id, string? reason, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        var sale = await _repo.FindByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException("La venta no existe.");

        var wasConfirmed = sale.Status == CounterSaleStatus.Confirmed;
        var now = _clock.GetUtcNow();

        // Validar antes de tocar ARCA o la base.
        if (wasConfirmed && string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException("El motivo de la devolución es obligatorio.");

        // Facturada: la nota de crédito va primero (es lo único que sale del sistema).
        if (wasConfirmed)
        {
            var invoice = await _invoicingRepo.FindAuthorizedInvoiceBySourceAsync(InvoiceSourceType.CounterSale, id, ct);
            if (invoice is not null)
            {
                var credit = await _voidInvoice.HandleAsync(invoice.Id, ct);
                if (credit.Status != InvoiceStatus.Authorized)
                    throw new BusinessRuleException(
                        $"ARCA rechazó la nota de crédito ({credit.RejectionReason ?? "sin detalle"}): la venta no se devolvió.");
            }
        }

        try
        {
            sale.Cancel(reason, now);
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
            // Contraasiento de stock: lo vendido vuelve.
            foreach (var item in sale.Items.Where(i => i.CatalogItemId.HasValue))
            {
                var catalogItem = await _catalogRepo.FindAnyByIdAsync(item.CatalogItemId!.Value, ct);
                if (catalogItem is null || !catalogItem.TrackStock) continue;

                var movement = new StockMovement(
                    tenantId, sale.BranchId, catalogItem.Id,
                    item.Quantity, StockMovementReason.CounterSaleCancel,
                    note: sale.CancelReason, workOrderItemId: null,
                    createdBy: _actor.ActorId, createdAt: now);

                catalogItem.ApplyStockMovement(item.Quantity);
                await _catalogRepo.AddMovementAsync(movement, ct);
            }

            // Contraasiento de caja: se devuelve lo que había entrado.
            if (sale.AccountId is { } accountId)
            {
                var account = await _cashRepo.FindAccountByIdAsync(tenantId, accountId, ct);
                if (account is not null)
                {
                    await _cashRepo.AddMovementAsync(new CashMovement(
                        tenantId, account.BranchId, account.Id,
                        sale.Total, CashMovementDirection.Out,
                        CashMovementReason.ManualExpense,
                        referenceType: "counter_sale_return", referenceId: sale.Id,
                        note: $"Devolución de la venta N° {sale.DocumentNumber}: {sale.CancelReason}",
                        createdByMembershipId: _actor.ActorId, createdAt: now), ct);
                }
            }
        }

        await _repo.SaveChangesAsync(ct);
        return CreateCounterSaleHandler.MapToDto(sale);
    }
}
