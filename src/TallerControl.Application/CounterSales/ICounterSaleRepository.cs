using TallerControl.Application.Common;
using TallerControl.Domain.CounterSales;

namespace TallerControl.Application.CounterSales;

public interface ICounterSaleRepository
{
    Task<CounterSale?> FindByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<Page<CounterSale>> SearchAsync(Guid tenantId, string? query, int page, int pageSize, CancellationToken ct);
    Task AddAsync(CounterSale sale, CancellationToken ct);
    Task<Guid?> GetMainBranchIdAsync(Guid tenantId, CancellationToken ct);
    Task<ICounterSaleTransaction> BeginTransactionAsync(CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
