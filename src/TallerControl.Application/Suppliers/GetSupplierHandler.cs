using TallerControl.Application.Common;

namespace TallerControl.Application.Suppliers;

public sealed class GetSupplierHandler
{
    private readonly ISupplierRepository _repo;
    private readonly ITenantContext _tenant;

    public GetSupplierHandler(ISupplierRepository repo, ITenantContext tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public async Task<SupplierDto> HandleAsync(Guid id, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");
        var supplier = await _repo.FindByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException("El proveedor no existe.");
        return CreateSupplierHandler.MapToDto(supplier) with
        {
            PurchaseCount = await _repo.CountOpenPurchasesAsync(tenantId, id, ct),
        };
    }
}
