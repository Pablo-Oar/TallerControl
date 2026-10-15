using TallerControl.Application.Common;

namespace TallerControl.Application.Purchases;

public sealed class GetPurchaseHandler
{
    private readonly IPurchaseRepository _repo;
    private readonly ITenantContext _tenant;

    public GetPurchaseHandler(IPurchaseRepository repo, ITenantContext tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public async Task<PurchaseDto> HandleAsync(Guid id, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");
        var purchase = await _repo.FindByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException("La compra no existe.");
        return CreatePurchaseHandler.MapToDto(purchase);
    }
}
