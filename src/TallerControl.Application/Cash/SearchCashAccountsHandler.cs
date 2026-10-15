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
        return accounts.Select(CreateCashAccountHandler.MapToDto).ToList();
    }
}
