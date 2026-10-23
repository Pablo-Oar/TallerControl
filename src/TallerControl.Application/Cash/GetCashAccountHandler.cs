using TallerControl.Application.Common;

namespace TallerControl.Application.Cash;

public sealed class GetCashAccountHandler
{
    private readonly ICashRepository _repo;
    private readonly ITenantContext _tenant;

    public GetCashAccountHandler(ICashRepository repo, ITenantContext tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public async Task<CashAccountDto> HandleAsync(Guid id, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");
        var account = await _repo.FindAccountByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException("La cuenta de caja no existe.");
        // El saldo es la suma de los movimientos: el detalle también lo informa (antes solo el listado).
        return CreateCashAccountHandler.MapToDto(account) with
        {
            Balance = await _repo.GetBalanceAsync(tenantId, id, ct),
        };
    }
}
