using TallerControl.Application.Common;
using TallerControl.Domain.Quotes;

namespace TallerControl.Application.Quotes;

/// <summary>Caso de uso: listar presupuestos paginados con filtros (doc 05 §2.6, T-QUO-01).</summary>
public sealed class SearchQuotesHandler
{
    private readonly IQuoteRepository _repo;

    public SearchQuotesHandler(IQuoteRepository repo) => _repo = repo;

    public Task<Page<QuoteListDto>> HandleAsync(
        QuoteStatus? status,
        Guid? customerId,
        Guid? vehicleId,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        SortSpec sort,
        CancellationToken ct)
        => _repo.SearchAsync(status, customerId, vehicleId, from, to, page, pageSize, sort, ct);
}
