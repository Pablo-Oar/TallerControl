using TallerControl.Application.Common;
using TallerControl.Domain.Customers;

namespace TallerControl.Application.Customers;

/// <summary>Caso de uso: crear cliente (doc 05 §2.3, T-CUS-02..04).</summary>
public sealed class CreateCustomerHandler
{
    private readonly ICustomerRepository _repo;
    private readonly ITenantContext _tenantContext;

    public CreateCustomerHandler(ICustomerRepository repo, ITenantContext tenantContext)
    {
        _repo = repo;
        _tenantContext = tenantContext;
    }

    public async Task<CustomerDto> HandleAsync(CustomerUpsertRequest request, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        await ValidateDocNumberAsync(null, request.DocType, request.DocNumber, ct);

        Customer customer;
        try
        {
            customer = new Customer(
                tenantId,
                request.Name,
                request.Email,
                request.Phone,
                request.DocType,
                request.DocNumber,
                request.VatCondition,
                request.Address,
                request.DoNotContact,
                request.Notes);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await _repo.AddAsync(customer, ct);
        await _repo.SaveChangesAsync(ct);

        return await _repo.GetFullAsync(customer.Id, ct)
            ?? throw new InvalidOperationException("El cliente recién creado no se pudo leer.");
    }

    private async Task ValidateDocNumberAsync(Guid? excludeId, DocType? docType, string? docNumber, CancellationToken ct)
    {
        if (docType is null || string.IsNullOrWhiteSpace(docNumber)) return;

        if (await _repo.DocNumberExistsAsync(excludeId, docType.Value, docNumber, ct))
            throw new ConflictException($"Ya existe un cliente con ese número de {docType}.");
    }
}
