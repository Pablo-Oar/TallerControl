using TallerControl.Application.Common;
using TallerControl.Domain.Quotes;

namespace TallerControl.Application.Quotes;

/// <summary>
/// Caso de uso: actualizar encabezado y renglones de un presupuesto (doc 05 §2.6, T-QUO-04).
/// Solo válido en estado Draft; usa xmin/If-Match para concurrencia optimista.
/// </summary>
public sealed class UpdateQuoteHandler
{
    private readonly IQuoteRepository _repo;

    public UpdateQuoteHandler(IQuoteRepository repo) => _repo = repo;

    public async Task<QuoteDto> HandleAsync(Guid id, uint xmin, UpdateQuoteRequest request, CancellationToken ct)
    {
        var quote = await _repo.FindByIdAsync(id, ct)
            ?? throw new NotFoundException("El presupuesto no existe.");

        if (quote.Version != xmin)
            throw new ConcurrencyException();

        var items = request.Items
            .Select((r, idx) => new QuoteItem(
                quote.TenantId, id,
                r.Kind, r.Description, r.CatalogItemId,
                r.Quantity, r.UnitNetPrice, r.VatRate,
                unitCostSnapshot: null,
                position: idx + 1,
                r.RequiredForApproval))
            .ToList();

        try
        {
            quote.Update(
                request.CustomerId, request.VehicleId, request.ValidUntil,
                request.GlobalDiscountPct,
                request.CustomerNotes, request.InternalNotes,
                items);
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
            ?? throw new InvalidOperationException("El presupuesto no se pudo leer tras la actualización.");
    }
}
