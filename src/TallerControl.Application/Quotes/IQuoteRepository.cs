using TallerControl.Application.Common;
using TallerControl.Domain.Quotes;

namespace TallerControl.Application.Quotes;

public interface IQuoteRepository
{
    Task<Page<QuoteListDto>> SearchAsync(
        QuoteStatus? status,
        Guid? customerId,
        Guid? vehicleId,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        SortSpec sort,
        CancellationToken ct);

    Task<QuoteDto?> GetFullAsync(Guid id, CancellationToken ct);

    /// <summary>Igual que <see cref="GetFullAsync"/> sin filtro de taller: para los endpoints públicos (el taller sale del enlace).</summary>
    Task<QuoteDto?> GetFullPublicAsync(Guid id, CancellationToken ct);

    Task<Quote?> FindByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Igual que <see cref="FindByIdAsync"/> sin filtro de taller: para los endpoints públicos.</summary>
    Task<Quote?> FindByIdPublicAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<QuoteResponseDto>> GetResponsesAsync(Guid quoteId, CancellationToken ct);

    Task AddAsync(Quote quote, CancellationToken ct);

    /// <summary>Elimina un renglón que se reemplazó al editar un borrador (sin esto EF rechaza el guardado).</summary>
    void DeleteItem(Domain.Quotes.QuoteItem item);

    Task SaveChangesAsync(CancellationToken ct);

    Task<IQuoteTransaction> BeginTransactionAsync(CancellationToken ct);
}
