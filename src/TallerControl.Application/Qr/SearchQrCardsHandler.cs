using TallerControl.Application.Common;
using TallerControl.Domain.Qr;

namespace TallerControl.Application.Qr;

public sealed class SearchQrCardsHandler(IQrRepository repo)
{
    public Task<Page<QrCardListDto>> HandleAsync(
        QrCardStatus? status, string? q, int page, int pageSize, CancellationToken ct) =>
        repo.SearchAsync(status, q, page, pageSize, ct);
}
