using TallerControl.Application.Common;

namespace TallerControl.Application.CounterSales;

public sealed class SearchCounterSalesHandler
{
    private readonly ICounterSaleRepository _repo;
    private readonly ITenantContext _tenant;

    public SearchCounterSalesHandler(ICounterSaleRepository repo, ITenantContext tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public async Task<Page<CounterSaleDto>> HandleAsync(string? query, int page, int pageSize, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");
        var result = await _repo.SearchAsync(tenantId, query, page, pageSize, ct);
        return new Page<CounterSaleDto>(
            result.Items.Select(CreateCounterSaleHandler.MapToDto).ToList(),
            result.PageNumber, result.PageSize, result.Total);
    }
}
