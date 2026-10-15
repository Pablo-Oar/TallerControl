using TallerControl.Application.Common;
using TallerControl.Domain.Cash;

namespace TallerControl.Application.Cash;

public sealed class CreateCashAccountHandler
{
    private readonly ICashRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly IBranchContext _branch;

    public CreateCashAccountHandler(ICashRepository repo, ITenantContext tenant, IBranchContext branch)
    {
        _repo = repo;
        _tenant = tenant;
        _branch = branch;
    }

    public async Task<CashAccountDto> HandleAsync(CreateCashAccountRequest request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        var branchId = _branch.BranchId
            ?? await _repo.GetMainBranchIdAsync(tenantId, ct)
            ?? throw new InvalidOperationException("El taller no tiene una sucursal principal configurada.");

        var account = new CashAccount(tenantId, branchId, request.Name, request.Kind);
        await _repo.AddAccountAsync(account, ct);
        await _repo.SaveChangesAsync(ct);
        return MapToDto(account);
    }

    internal static CashAccountDto MapToDto(CashAccount a) =>
        new(a.Id, a.BranchId, a.Name, a.Kind, a.IsActive, a.Version);
}
