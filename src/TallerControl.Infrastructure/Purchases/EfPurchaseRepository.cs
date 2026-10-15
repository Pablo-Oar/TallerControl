using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Common;
using TallerControl.Application.Purchases;
using TallerControl.Domain.Purchases;
using TallerControl.Domain.Tenancy;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Purchases;

internal sealed class EfPurchaseRepository : IPurchaseRepository
{
    private readonly AppDbContext _db;

    public EfPurchaseRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Purchase?> FindByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        _db.Set<Purchase>()
            .Include(p => p.Items)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId && p.DeletedAt == null, ct);

    public async Task<Page<Purchase>> SearchAsync(
        Guid tenantId, string? query, int page, int pageSize, CancellationToken ct)
    {
        IQueryable<Purchase> q = _db.Set<Purchase>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query) && int.TryParse(query.Trim(), out var num))
            q = q.Where(p => p.DocumentNumber == num);

        var total = await q.CountAsync(ct);

        var items = await q
            .Include(p => p.Items)
            .OrderByDescending(p => p.InvoiceDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new Page<Purchase>(items, page, pageSize, total);
    }

    public async Task AddAsync(Purchase purchase, CancellationToken ct) =>
        await _db.Set<Purchase>().AddAsync(purchase, ct);

    public Task<Guid?> GetMainBranchIdAsync(Guid tenantId, CancellationToken ct) =>
        _db.Set<Branch>()
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.IsMain)
            .Select(b => (Guid?)b.Id)
            .FirstOrDefaultAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
