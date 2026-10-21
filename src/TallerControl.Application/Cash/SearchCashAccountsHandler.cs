using TallerControl.Application.Common;

namespace TallerControl.Application.Cash;

public sealed class SearchCashAccountsHandler
{
    private readonly ICashRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly IBranchContext _branch;

    public SearchCashAccountsHandler(ICashRepository repo, ITenantContext tenant, IBranchContext branch)
    {
        _repo = repo;
        _tenant = tenant;
        _branch = branch;
    }

    public async Task<List<CashAccountDto>> HandleAsync(CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");
        var branchId = _branch.BranchId
            ?? await _repo.GetMainBranchIdAsync(tenantId, ct)
            ?? throw new InvalidOperationException("El taller no tiene una sucursal principal configurada.");
        var accounts = await _repo.SearchAccountsAsync(tenantId, branchId, ct);

        var result = new List<CashAccountDto>(accounts.Count);
        foreach (var a in accounts)
        {
            var balance = await _repo.GetBalanceAsync(tenantId, a.Id, ct);
            var open = await _repo.FindOpenSessionAsync(tenantId, a.Id, ct);
            result.Add(CreateCashAccountHandler.MapToDto(a) with { Balance = balance, SessionOpenedAt = open?.OpenedAt });
        }
        return result;
    }
}
