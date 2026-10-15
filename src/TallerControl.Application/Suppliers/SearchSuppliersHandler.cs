using TallerControl.Application.Common;

namespace TallerControl.Application.Suppliers;

public sealed class SearchSuppliersHandler
{
    private readonly ISupplierRepository _repo;
    private readonly ITenantContext _tenant;

    public SearchSuppliersHandler(ISupplierRepository repo, ITenantContext tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public async Task<Page<SupplierDto>> HandleAsync(string? query, int page, int pageSize, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");
        var result = await _repo.SearchAsync(tenantId, query, page, pageSize, ct);
        return new Page<SupplierDto>(
            result.Items.Select(CreateSupplierHandler.MapToDto).ToList(),
            result.PageNumber, result.PageSize, result.Total);
    }
}
