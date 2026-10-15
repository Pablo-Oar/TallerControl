using TallerControl.Application.Common;
using TallerControl.Domain.Purchases;

namespace TallerControl.Application.Purchases;

public interface IPurchaseRepository
{
    Task<Purchase?> FindByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<Page<Purchase>> SearchAsync(Guid tenantId, string? query, int page, int pageSize, CancellationToken ct);
    Task AddAsync(Purchase purchase, CancellationToken ct);
    Task<Guid?> GetMainBranchIdAsync(Guid tenantId, CancellationToken ct);
    Task<IPurchaseTransaction> BeginTransactionAsync(CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
