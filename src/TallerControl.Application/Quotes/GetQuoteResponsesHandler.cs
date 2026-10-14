using TallerControl.Application.Common;

namespace TallerControl.Application.Quotes;

/// <summary>Caso de uso: obtener historial de respuestas de un presupuesto (doc 05 §2.6).</summary>
public sealed class GetQuoteResponsesHandler
{
    private readonly IQuoteRepository _repo;

    public GetQuoteResponsesHandler(IQuoteRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<QuoteResponseDto>> HandleAsync(Guid quoteId, CancellationToken ct)
    {
        var quote = await _repo.GetFullAsync(quoteId, ct)
            ?? throw new NotFoundException("El presupuesto no existe.");

        _ = quote; // confirma existencia y aislamiento de taller
        return await _repo.GetResponsesAsync(quoteId, ct);
    }
}
