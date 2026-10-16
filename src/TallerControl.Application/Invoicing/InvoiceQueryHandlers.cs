using TallerControl.Application.Common;
using TallerControl.Domain.Invoicing;

namespace TallerControl.Application.Invoicing;

public sealed class GetInvoiceHandler
{
    private readonly IInvoicingRepository _repo;

    public GetInvoiceHandler(IInvoicingRepository repo) => _repo = repo;

    public async Task<InvoiceDto> HandleAsync(Guid id, CancellationToken ct)
    {
        return await _repo.GetInvoiceDtoAsync(id, ct)
            ?? throw new NotFoundException();
    }
}

public sealed class SearchInvoicesHandler
{
    private readonly IInvoicingRepository _repo;

    public SearchInvoicesHandler(IInvoicingRepository repo) => _repo = repo;

    public Task<Page<InvoiceListDto>> HandleAsync(
        InvoiceStatus? status,
        InvoiceSourceType? sourceType,
        int page,
        int pageSize,
        CancellationToken ct)
        => _repo.SearchInvoicesAsync(status, sourceType, page, pageSize, ct);
}

public sealed class ListFiscalCertificatesHandler
{
    private readonly IInvoicingRepository _repo;

    public ListFiscalCertificatesHandler(IInvoicingRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<FiscalCertificateDto>> HandleAsync(CancellationToken ct)
    {
        var list = await _repo.ListCertificatesAsync(ct);
        return list.Select(CreateFiscalCertificateHandler.MapToDto).ToList();
    }
}
