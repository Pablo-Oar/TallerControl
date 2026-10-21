using TallerControl.Application.Common;
using TallerControl.Domain.Customers;

namespace TallerControl.Application.Customers;

public interface ICustomerRepository
{
    Task<Page<CustomerListDto>> SearchAsync(string? q, int page, int pageSize, SortSpec sort, bool includeDeleted, CancellationToken ct);

    Task<CustomerDto?> GetFullAsync(Guid id, CancellationToken ct);

    Task<Customer?> FindActiveByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Busca también entre los borrados, para restaurar.</summary>
    Task<Customer?> FindAnyByIdAsync(Guid id, CancellationToken ct);

    Task<bool> DocNumberExistsAsync(Guid? excludeId, DocType docType, string docNumber, CancellationToken ct);

    /// <summary>Suma de saldos de órdenes no canceladas (implementado en E4; devuelve 0 en E3).</summary>
    Task<decimal> GetPendingBalanceAsync(Guid customerId, CancellationToken ct);

    Task AddAsync(Customer customer, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
