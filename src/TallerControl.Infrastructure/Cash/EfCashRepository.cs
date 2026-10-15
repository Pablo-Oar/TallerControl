using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Cash;
using TallerControl.Domain.Cash;
using TallerControl.Domain.Tenancy;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Cash;

internal sealed class EfCashRepository : ICashRepository
{
    private readonly AppDbContext _db;

    public EfCashRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Guid?> GetMainBranchIdAsync(Guid tenantId, CancellationToken ct) =>
        _db.Set<Branch>()
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.IsMain)
            .Select(b => (Guid?)b.Id)
            .FirstOrDefaultAsync(ct);

    public Task<CashAccount?> FindAccountByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        _db.Set<CashAccount>()
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId && a.DeletedAt == null, ct);

    public async Task<List<CashAccount>> SearchAccountsAsync(
        Guid tenantId, Guid branchId, CancellationToken ct) =>
        await _db.Set<CashAccount>()
            .AsNoTracking()
            .Where(a => a.BranchId == branchId)
            .OrderBy(a => a.Name)
            .ToListAsync(ct);

    public async Task AddAccountAsync(CashAccount account, CancellationToken ct) =>
        await _db.Set<CashAccount>().AddAsync(account, ct);

    public Task<CashSession?> FindSessionByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        _db.Set<CashSession>()
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<CashSession?> FindOpenSessionAsync(Guid tenantId, Guid accountId, CancellationToken ct) =>
        _db.Set<CashSession>()
            .FirstOrDefaultAsync(s => s.AccountId == accountId && s.Status == CashSessionStatus.Open, ct);

    public async Task AddSessionAsync(CashSession session, CancellationToken ct) =>
        await _db.Set<CashSession>().AddAsync(session, ct);

    public async Task AddMovementAsync(CashMovement movement, CancellationToken ct) =>
        await _db.Set<CashMovement>().AddAsync(movement, ct);

    public async Task<List<CashMovement>> SearchMovementsAsync(
        Guid tenantId, Guid accountId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)
    {
        var query = _db.Set<CashMovement>()
            .AsNoTracking()
            .Where(m => m.AccountId == accountId);

        if (from.HasValue) query = query.Where(m => m.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(m => m.CreatedAt <= to.Value);

        return await query.OrderByDescending(m => m.CreatedAt).ToListAsync(ct);
    }

    public async Task<decimal> GetBalanceAsync(Guid tenantId, Guid accountId, CancellationToken ct)
    {
        var ins = await _db.Set<CashMovement>()
            .AsNoTracking()
            .Where(m => m.AccountId == accountId && m.Direction == CashMovementDirection.In)
            .SumAsync(m => m.Amount, ct);

        var outs = await _db.Set<CashMovement>()
            .AsNoTracking()
            .Where(m => m.AccountId == accountId && m.Direction == CashMovementDirection.Out)
            .SumAsync(m => m.Amount, ct);

        return ins - outs;
    }

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
