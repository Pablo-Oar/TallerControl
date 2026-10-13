using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Catalog;
using TallerControl.Application.Common;
using TallerControl.Domain.Catalog;
using TallerControl.Domain.Tenancy;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Catalog;

internal sealed class EfCatalogRepository : ICatalogRepository
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public EfCatalogRepository(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Page<CatalogItemListDto>> SearchAsync(
        string? q, CatalogItemKind? kind, bool? activeOnly, int page, int pageSize, CancellationToken ct)
    {
        var query = _db.Set<CatalogItem>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c =>
                EF.Functions.ILike(c.Name, $"%{term}%") ||
                (c.Sku != null && EF.Functions.ILike(c.Sku, $"%{term}%")));
        }

        if (kind.HasValue)
            query = query.Where(c => c.Kind == kind.Value);

        if (activeOnly.HasValue)
            query = query.Where(c => c.IsActive == activeOnly.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CatalogItemListDto(
                c.Id,
                c.Kind,
                c.Name,
                c.Sku,
                c.NetPrice,
                c.VatRate,
                c.Cost,
                c.TrackStock,
                c.StockQty,
                c.StockMin,
                c.IsActive,
                c.TrackStock && c.StockMin.HasValue && c.StockQty < c.StockMin.Value))
            .ToListAsync(ct);

        return new Page<CatalogItemListDto>(items, page, pageSize, total);
    }

    public async Task<CatalogItemDto?> GetFullAsync(Guid id, CancellationToken ct)
    {
        var item = await _db.Set<CatalogItem>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _tenantContext.TenantId && c.DeletedAt == null, ct);

        if (item is null) return null;

        return new CatalogItemDto(
            item.Id,
            item.Kind,
            item.Name,
            item.Sku,
            item.NetPrice,
            item.VatRate,
            item.Cost,
            item.TrackStock,
            item.StockQty,
            item.StockMin,
            item.DurationMinutes,
            item.BufferMinutes,
            item.IsActive,
            item.IsLowStock,
            item.Version);
    }

    public Task<CatalogItem?> FindActiveByIdAsync(Guid id, CancellationToken ct) =>
        _db.Set<CatalogItem>().FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<CatalogItem?> FindAnyByIdAsync(Guid id, CancellationToken ct) =>
        _db.Set<CatalogItem>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _tenantContext.TenantId, ct);

    public Task<bool> SkuExistsAsync(Guid? excludeId, string sku, CancellationToken ct)
    {
        var normalized = sku.Trim().ToUpperInvariant();
        return _db.Set<CatalogItem>()
            .AnyAsync(c => c.Sku == normalized && (excludeId == null || c.Id != excludeId.Value), ct);
    }

    public async Task<List<StockMovementDto>> GetMovementsAsync(
        Guid catalogItemId, int page, int pageSize, CancellationToken ct)
    {
        return await _db.Set<StockMovement>()
            .AsNoTracking()
            .Where(s => s.CatalogItemId == catalogItemId)
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StockMovementDto(
                s.Id,
                s.Quantity,
                s.Reason,
                s.Note,
                s.WorkOrderItemId,
                s.CreatedBy,
                s.CreatedAt))
            .ToListAsync(ct);
    }

    public Task<Guid?> GetMainBranchIdAsync(Guid tenantId, CancellationToken ct) =>
        _db.Set<Branch>()
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.IsMain)
            .Select(b => (Guid?)b.Id)
            .FirstOrDefaultAsync(ct);

    public async Task AddAsync(CatalogItem item, CancellationToken ct) =>
        await _db.Set<CatalogItem>().AddAsync(item, ct);

    public async Task AddMovementAsync(StockMovement movement, CancellationToken ct) =>
        await _db.Set<StockMovement>().AddAsync(movement, ct);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
