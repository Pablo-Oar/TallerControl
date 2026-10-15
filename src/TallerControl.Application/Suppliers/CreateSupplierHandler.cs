using TallerControl.Application.Common;
using TallerControl.Domain.Suppliers;

namespace TallerControl.Application.Suppliers;

public sealed class CreateSupplierHandler
{
    private readonly ISupplierRepository _repo;
    private readonly ITenantContext _tenant;

    public CreateSupplierHandler(ISupplierRepository repo, ITenantContext tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public async Task<SupplierDto> HandleAsync(CreateSupplierRequest request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        Supplier supplier;
        try
        {
            supplier = new Supplier(
                tenantId, request.Name, request.Cuit, request.VatCondition,
                request.Address, request.Phone, request.Email, request.Notes);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await _repo.AddAsync(supplier, ct);
        await _repo.SaveChangesAsync(ct);
        return MapToDto(supplier);
    }

    internal static SupplierDto MapToDto(Supplier s) =>
        new(s.Id, s.Name, s.Cuit, s.VatCondition, s.Address, s.Phone, s.Email, s.Notes, s.Version);
}
