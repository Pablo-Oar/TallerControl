using TallerControl.Application.Common;

namespace TallerControl.Application.Purchases;

public sealed class SearchPurchasesHandler
{
    private readonly IPurchaseRepository _repo;
    private readonly ITenantContext _tenant;

    public SearchPurchasesHandler(IPurchaseRepository repo, ITenantContext tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public async Task<Page<PurchaseDto>> HandleAsync(string? query, int page, int pageSize, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");
        var result = await _repo.SearchAsync(tenantId, query, page, pageSize, ct);
        return new Page<PurchaseDto>(
            result.Items.Select(CreatePurchaseHandler.MapToDto).ToList(),
            result.PageNumber, result.PageSize, result.Total);
    }
}
