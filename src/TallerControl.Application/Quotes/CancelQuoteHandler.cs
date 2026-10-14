using TallerControl.Application.Common;

namespace TallerControl.Application.Quotes;

/// <summary>
/// Caso de uso: cancelar un presupuesto (doc 05 §2.6, T-QUO-07).
/// Se puede cancelar desde cualquier estado no terminal.
/// </summary>
public sealed class CancelQuoteHandler
{
    private readonly IQuoteRepository _repo;

    public CancelQuoteHandler(IQuoteRepository repo) => _repo = repo;

    public async Task<QuoteDto> HandleAsync(Guid id, CancelQuoteRequest request, CancellationToken ct)
    {
        var quote = await _repo.FindByIdAsync(id, ct)
            ?? throw new NotFoundException("El presupuesto no existe.");

        try
        {
            quote.Cancel(request.Reason);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);

        return await _repo.GetFullAsync(id, ct)
            ?? throw new InvalidOperationException("El presupuesto no se pudo leer tras la cancelación.");
    }
}
