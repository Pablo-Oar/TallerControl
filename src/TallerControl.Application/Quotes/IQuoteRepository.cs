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
        CancellationToken ct);

    Task<QuoteDto?> GetFullAsync(Guid id, CancellationToken ct);

    Task<Quote?> FindByIdAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<QuoteResponseDto>> GetResponsesAsync(Guid quoteId, CancellationToken ct);

    Task AddAsync(Quote quote, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);

    Task<IQuoteTransaction> BeginTransactionAsync(CancellationToken ct);
}
