using TallerControl.Application.Common;

namespace TallerControl.Application.Quotes;

/// <summary>
/// Caso de uso: reabrir un presupuesto enviado o rechazado (Sent/Rejected → Draft, doc 05 §2.6, T-QUO-06).
/// Invalida las respuestas vigentes.
/// </summary>
public sealed class ReopenQuoteHandler
{
    private readonly IQuoteRepository _repo;

    public ReopenQuoteHandler(IQuoteRepository repo) => _repo = repo;

    public async Task<QuoteDto> HandleAsync(Guid id, ReopenQuoteRequest request, CancellationToken ct)
    {
        var quote = await _repo.FindByIdAsync(id, ct)
            ?? throw new NotFoundException("El presupuesto no existe.");

        try
        {
            quote.Reopen(request.Reason);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);

        return await _repo.GetFullAsync(id, ct)
            ?? throw new InvalidOperationException("El presupuesto no se pudo leer tras reabrirlo.");
    }
}
