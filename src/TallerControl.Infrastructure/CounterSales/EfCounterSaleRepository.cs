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

        if (!string.IsNullOrWhiteSpace(query))
        {
            var text = query.Trim();
            if (int.TryParse(text, out var num))
                q = q.Where(s => s.DocumentNumber == num);
            else
            {
                // Texto libre: ventas que tengan un renglón con esa descripción.
                var pattern = $"%{text}%";
                q = q.Where(s => s.Items.Any(i => EF.Functions.ILike(i.Description, pattern)));
            }
        }

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

    public async Task<ICounterSaleTransaction> BeginTransactionAsync(CancellationToken ct)
    {
        var tx = await _db.Database.BeginTransactionAsync(ct);
        return new EfCounterSaleTransaction(tx);
    }

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);

    private sealed class EfCounterSaleTransaction : ICounterSaleTransaction
    {
        private readonly Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _tx;

        public EfCounterSaleTransaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx) => _tx = tx;

        public Task CommitAsync(CancellationToken ct) => _tx.CommitAsync(ct);

        public ValueTask DisposeAsync() => _tx.DisposeAsync();
    }
}
