using TallerControl.Application.Common;
using TallerControl.Domain.Catalog;

namespace TallerControl.Application.Catalog;

public interface ICatalogRepository
{
    Task<Page<CatalogItemListDto>> SearchAsync(string? q, CatalogItemKind? kind, bool? activeOnly, int page, int pageSize, SortSpec sort, CancellationToken ct);

    Task<CatalogItemDto?> GetFullAsync(Guid id, CancellationToken ct);

    Task<CatalogItem?> FindActiveByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Busca también entre los borrados lógicamente.</summary>
    Task<CatalogItem?> FindAnyByIdAsync(Guid id, CancellationToken ct);

    Task<bool> SkuExistsAsync(Guid? excludeId, string sku, CancellationToken ct);

    Task<List<StockMovementDto>> GetMovementsAsync(Guid catalogItemId, int page, int pageSize, CancellationToken ct);

    /// <summary>Devuelve el ID de la sucursal principal del taller (para cuando IBranchContext no tiene una activa).</summary>
    Task<Guid?> GetMainBranchIdAsync(Guid tenantId, CancellationToken ct);

    Task AddAsync(CatalogItem item, CancellationToken ct);

    Task AddMovementAsync(StockMovement movement, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
