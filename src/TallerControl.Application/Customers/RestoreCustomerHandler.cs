using TallerControl.Application.Common;

namespace TallerControl.Application.Customers;

/// <summary>Caso de uso: restaurar cliente borrado (doc 05 §2.3, T-CUS-08).</summary>
public sealed class RestoreCustomerHandler
{
    private readonly ICustomerRepository _repo;

    public RestoreCustomerHandler(ICustomerRepository repo) => _repo = repo;

    public async Task<CustomerDto> HandleAsync(Guid id, CancellationToken ct)
    {
        var customer = await _repo.FindAnyByIdAsync(id, ct)
            ?? throw new NotFoundException();

        if (!customer.IsDeleted)
            throw new ConflictException("El cliente no está borrado.");

        // Si volvió a haber otro cliente con el mismo número de documento, se rechaza.
        if (customer.DocType.HasValue && customer.DocNumber is not null)
        {
            if (await _repo.DocNumberExistsAsync(id, customer.DocType.Value, customer.DocNumber, ct))
                throw new ConflictException(
                    $"No se puede restaurar: el número de {customer.DocType} ya está en uso por otro cliente.");
        }

        customer.Restore();
        await _repo.SaveChangesAsync(ct);

        return await _repo.GetFullAsync(id, ct)
            ?? throw new InvalidOperationException("El cliente restaurado no se pudo leer.");
    }
}
