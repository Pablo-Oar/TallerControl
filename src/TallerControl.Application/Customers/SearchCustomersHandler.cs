using TallerControl.Application.Common;

namespace TallerControl.Application.Customers;

/// <summary>Caso de uso: buscar clientes (doc 05 §2.3, T-CUS-01).</summary>
public sealed class SearchCustomersHandler
{
    private readonly ICustomerRepository _repo;

    public SearchCustomersHandler(ICustomerRepository repo) => _repo = repo;

    public Task<Page<CustomerListDto>> HandleAsync(string? q, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        return _repo.SearchAsync(q, page, pageSize, ct);
    }
}
