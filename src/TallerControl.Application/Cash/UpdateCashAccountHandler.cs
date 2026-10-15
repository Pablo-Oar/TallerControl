using TallerControl.Application.Common;

namespace TallerControl.Application.Cash;

public sealed class UpdateCashAccountHandler
{
    private readonly ICashRepository _repo;
    private readonly ITenantContext _tenant;

    public UpdateCashAccountHandler(ICashRepository repo, ITenantContext tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public async Task<CashAccountDto> HandleAsync(Guid id, UpdateCashAccountRequest request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");
        var account = await _repo.FindAccountByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException("La cuenta de caja no existe.");

        if (account.Version != request.Version)
            throw new ConcurrencyException();

        account.Update(request.Name, request.Kind, request.IsActive);
        await _repo.SaveChangesAsync(ct);
        return CreateCashAccountHandler.MapToDto(account);
    }
}
