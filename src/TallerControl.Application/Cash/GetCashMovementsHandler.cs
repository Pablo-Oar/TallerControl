using TallerControl.Application.Common;

namespace TallerControl.Application.Cash;

public sealed class GetCashMovementsHandler
{
    private readonly ICashRepository _repo;
    private readonly ITenantContext _tenant;

    public GetCashMovementsHandler(ICashRepository repo, ITenantContext tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public async Task<(List<CashMovementDto> Movements, decimal Balance)> HandleAsync(
        Guid accountId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        _ = await _repo.FindAccountByIdAsync(tenantId, accountId, ct)
            ?? throw new NotFoundException("La cuenta de caja no existe.");

        var movements = await _repo.SearchMovementsAsync(tenantId, accountId, from, to, ct);
        var balance = await _repo.GetBalanceAsync(tenantId, accountId, ct);

        return (movements.Select(ManualCashMovementHandler.MapToDto).ToList(), balance);
    }
}
