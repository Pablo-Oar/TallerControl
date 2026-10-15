using TallerControl.Application.Common;
using TallerControl.Domain.Suppliers;

namespace TallerControl.Application.Suppliers;

public interface ISupplierRepository
{
    Task<Supplier?> FindByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<Page<Supplier>> SearchAsync(Guid tenantId, string? query, int page, int pageSize, CancellationToken ct);
    Task AddAsync(Supplier supplier, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
