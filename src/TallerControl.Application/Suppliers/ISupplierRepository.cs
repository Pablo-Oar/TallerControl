using TallerControl.Application.Common;
using TallerControl.Domain.Suppliers;

namespace TallerControl.Application.Suppliers;

public interface ISupplierRepository
{
    Task<Supplier?> FindByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<Supplier?> FindDeletedByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    /// <param name="deleted">true = solo los dados de baja.</param>
    Task<Page<Supplier>> SearchAsync(Guid tenantId, string? query, bool deleted, int page, int pageSize, CancellationToken ct);

    /// <summary>Compras del proveedor que siguen vigentes (en borrador o confirmadas).</summary>
    Task<int> CountOpenPurchasesAsync(Guid tenantId, Guid supplierId, CancellationToken ct);
    Task AddAsync(Supplier supplier, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
