using TallerControl.Application.Common;

namespace TallerControl.Application.Suppliers;

public sealed class UpdateSupplierHandler
{
    private readonly ISupplierRepository _repo;
    private readonly ITenantContext _tenant;

    public UpdateSupplierHandler(ISupplierRepository repo, ITenantContext tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public async Task<SupplierDto> HandleAsync(Guid id, UpdateSupplierRequest request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");
        var supplier = await _repo.FindByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException("El proveedor no existe.");

        if (supplier.Version != request.Version)
            throw new ConcurrencyException();

        try
        {
            supplier.Update(request.Name, request.Cuit, request.VatCondition,
                request.Address, request.Phone, request.Email, request.Notes);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        return CreateSupplierHandler.MapToDto(supplier);
    }
}
