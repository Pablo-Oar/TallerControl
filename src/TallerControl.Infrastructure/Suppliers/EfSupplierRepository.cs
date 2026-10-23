using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Common;
using TallerControl.Application.Suppliers;
using TallerControl.Domain.Suppliers;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Suppliers;

internal sealed class EfSupplierRepository : ISupplierRepository
{
    private readonly AppDbContext _db;

    public EfSupplierRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Supplier?> FindByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        _db.Set<Supplier>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId && s.DeletedAt == null, ct);

    public Task<Supplier?> FindDeletedByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        _db.Set<Supplier>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId && s.DeletedAt != null, ct);

    public async Task<Page<Supplier>> SearchAsync(
        Guid tenantId, string? query, bool deleted, int page, int pageSize, CancellationToken ct)
    {
        var q = deleted
            ? _db.Set<Supplier>().IgnoreQueryFilters().AsNoTracking().Where(s => s.TenantId == tenantId && s.DeletedAt != null)
            : _db.Set<Supplier>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            q = q.Where(s =>
                EF.Functions.ILike(s.Name, $"%{term}%") ||
                (s.Cuit != null && EF.Functions.ILike(s.Cuit, $"%{term}%")));
        }

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new Page<Supplier>(items, page, pageSize, total);
    }

    public Task<int> CountOpenPurchasesAsync(Guid tenantId, Guid supplierId, CancellationToken ct) =>
        _db.Set<TallerControl.Domain.Purchases.Purchase>()
            .CountAsync(p => p.SupplierId == supplierId
                             && p.Status != TallerControl.Domain.Purchases.PurchaseStatus.Cancelled, ct);

    public async Task AddAsync(Supplier supplier, CancellationToken ct) =>
        await _db.Set<Supplier>().AddAsync(supplier, ct);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
