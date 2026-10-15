using TallerControl.Domain.Cash;

namespace TallerControl.Application.Cash;

public interface ICashRepository
{
    Task<Guid?> GetMainBranchIdAsync(Guid tenantId, CancellationToken ct);

    // CashAccount
    Task<CashAccount?> FindAccountByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<List<CashAccount>> SearchAccountsAsync(Guid tenantId, Guid branchId, CancellationToken ct);
    Task AddAccountAsync(CashAccount account, CancellationToken ct);

    // CashSession
    Task<CashSession?> FindSessionByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<CashSession?> FindOpenSessionAsync(Guid tenantId, Guid accountId, CancellationToken ct);
    Task AddSessionAsync(CashSession session, CancellationToken ct);

    // CashMovement
    Task AddMovementAsync(CashMovement movement, CancellationToken ct);
    Task<List<CashMovement>> SearchMovementsAsync(
        Guid tenantId, Guid accountId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct);
    Task<decimal> GetBalanceAsync(Guid tenantId, Guid accountId, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
