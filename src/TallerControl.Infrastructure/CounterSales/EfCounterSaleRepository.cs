using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Common;
using TallerControl.Application.CounterSales;
using TallerControl.Domain.CounterSales;
using TallerControl.Domain.Tenancy;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.CounterSales;

internal sealed class EfCounterSaleRepository : ICounterSaleRepository
{
    private readonly AppDbContext _db;

    public EfCounterSaleRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<CounterSale?> FindByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        _db.Set<CounterSale>()
            .Include(s => s.Items)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId && s.DeletedAt == null, ct);

    public async Task<Page<CounterSale>> SearchAsync(
        Guid tenantId, string? query, int page, int pageSize, CancellationToken ct)
    {
        IQueryable<CounterSale> q = _db.Set<CounterSale>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query) && int.TryParse(query.Trim(), out var num))
            q = q.Where(s => s.DocumentNumber == num);

        var total = await q.CountAsync(ct);

        var items = await q
            .Include(s => s.Items)
            .OrderByDescending(s => s.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new Page<CounterSale>(items, page, pageSize, total);
    }

    public async Task AddAsync(CounterSale sale, CancellationToken ct) =>
        await _db.Set<CounterSale>().AddAsync(sale, ct);

    public Task<Guid?> GetMainBranchIdAsync(Guid tenantId, CancellationToken ct) =>
        _db.Set<Branch>()
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.IsMain)
            .Select(b => (Guid?)b.Id)
            .FirstOrDefaultAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
