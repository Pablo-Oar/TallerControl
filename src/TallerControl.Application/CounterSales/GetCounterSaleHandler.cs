using TallerControl.Application.Common;

namespace TallerControl.Application.CounterSales;

public sealed class GetCounterSaleHandler
{
    private readonly ICounterSaleRepository _repo;
    private readonly ITenantContext _tenant;

    public GetCounterSaleHandler(ICounterSaleRepository repo, ITenantContext tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public async Task<CounterSaleDto> HandleAsync(Guid id, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");
        var sale = await _repo.FindByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException("La venta no existe.");
        return CreateCounterSaleHandler.MapToDto(sale);
    }
}
