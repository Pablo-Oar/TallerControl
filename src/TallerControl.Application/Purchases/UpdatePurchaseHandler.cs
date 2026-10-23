using TallerControl.Application.Common;
using TallerControl.Domain.Purchases;

namespace TallerControl.Application.Purchases;

/// <summary>K-03: corrige una compra en borrador (datos de la factura y renglones). Una confirmada no se edita: se anula.</summary>
public sealed class UpdatePurchaseHandler(IPurchaseRepository repo, ITenantContext tenant)
{
    public async Task<PurchaseDto> HandleAsync(Guid id, UpdatePurchaseRequest request, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        var purchase = await repo.FindByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException("La compra no existe.");

        if (purchase.Version != request.Version)
            throw new ConcurrencyException();

        if (request.Items.Count == 0)
            throw new BusinessRuleException("La compra debe tener al menos un ítem.");

        try
        {
            purchase.UpdateHeader(
                request.InvoiceDate.ToUniversalTime(), request.PointOfSale, request.InvoiceNumber,
                request.Perceptions, request.Retentions);

            foreach (var removed in purchase.ClearItems())
                repo.DeleteItem(removed);

            foreach (var itemReq in request.Items)
            {
                purchase.AddItem(new PurchaseItem(
                    tenantId, purchase.Id, itemReq.CatalogItemId,
                    itemReq.Description, itemReq.Quantity, itemReq.UnitCost, itemReq.VatRate));
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

        await repo.SaveChangesAsync(ct);
        return CreatePurchaseHandler.MapToDto(purchase);
    }
}
