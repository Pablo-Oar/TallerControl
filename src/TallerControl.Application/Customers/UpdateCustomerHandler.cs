using TallerControl.Application.Common;
using TallerControl.Domain.Customers;

namespace TallerControl.Application.Customers;

/// <summary>Caso de uso: editar cliente con concurrencia optimista (doc 05 §2.3, T-CUS-06).</summary>
public sealed class UpdateCustomerHandler
{
    private readonly ICustomerRepository _repo;

    public UpdateCustomerHandler(ICustomerRepository repo) => _repo = repo;

    public async Task<CustomerDto> HandleAsync(Guid id, uint ifMatchVersion, CustomerUpsertRequest request, CancellationToken ct)
    {
        var customer = await _repo.FindActiveByIdAsync(id, ct)
            ?? throw new NotFoundException();

        if (customer.Version != ifMatchVersion)
            throw new ConcurrencyException();

        await ValidateDocNumberAsync(id, request.DocType, request.DocNumber, ct);

        try
        {
            customer.Update(
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

        await _repo.SaveChangesAsync(ct);

        return await _repo.GetFullAsync(id, ct)
            ?? throw new InvalidOperationException("El cliente no se pudo leer después de la edición.");
    }

    private async Task ValidateDocNumberAsync(Guid excludeId, DocType? docType, string? docNumber, CancellationToken ct)
    {
        if (docType is null || string.IsNullOrWhiteSpace(docNumber)) return;

        if (await _repo.DocNumberExistsAsync(excludeId, docType.Value, docNumber, ct))
            throw new ConflictException($"Ya existe un cliente con ese número de {docType}.");
    }
}
