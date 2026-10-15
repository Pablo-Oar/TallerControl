using TallerControl.Application.Common;

namespace TallerControl.Application.Suppliers;

public sealed class RestoreSupplierHandler
{
    private readonly ISupplierRepository _repo;
    private readonly ITenantContext _tenant;

    public RestoreSupplierHandler(ISupplierRepository repo, ITenantContext tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public async Task<SupplierDto> HandleAsync(Guid id, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");
        var supplier = await _repo.FindDeletedByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException("El proveedor no existe.");

        if (!supplier.IsDeleted)
            throw new ConflictException("El proveedor no está eliminado.");

        supplier.Restore();
        await _repo.SaveChangesAsync(ct);
        return CreateSupplierHandler.MapToDto(supplier);
    }
}
