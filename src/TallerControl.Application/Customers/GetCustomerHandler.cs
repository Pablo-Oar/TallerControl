using TallerControl.Application.Common;

namespace TallerControl.Application.Customers;

/// <summary>Caso de uso: ver ficha del cliente (doc 05 §2.3, T-CUS-05, T-ISO-01).</summary>
public sealed class GetCustomerHandler
{
    private readonly ICustomerRepository _repo;

    public GetCustomerHandler(ICustomerRepository repo) => _repo = repo;

    public async Task<CustomerDto> HandleAsync(Guid id, CancellationToken ct)
    {
        return await _repo.GetFullAsync(id, ct) ?? throw new NotFoundException();
    }
}
