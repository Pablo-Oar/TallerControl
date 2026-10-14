using TallerControl.Application.Common;

namespace TallerControl.Application.Quotes;

/// <summary>Caso de uso: obtener detalle de un presupuesto (doc 05 §2.6, T-QUO-01).</summary>
public sealed class GetQuoteHandler
{
    private readonly IQuoteRepository _repo;

    public GetQuoteHandler(IQuoteRepository repo) => _repo = repo;

    public async Task<QuoteDto> HandleAsync(Guid id, CancellationToken ct)
    {
        return await _repo.GetFullAsync(id, ct)
            ?? throw new NotFoundException("El presupuesto no existe.");
    }
}
