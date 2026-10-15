using TallerControl.Application.Common;
using TallerControl.Domain.Cash;

namespace TallerControl.Application.Cash;

public sealed class ManualCashMovementHandler
{
    private readonly ICashRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly TimeProvider _clock;

    public ManualCashMovementHandler(ICashRepository repo, ITenantContext tenant, IActorContext actor, TimeProvider clock)
    {
        _repo = repo;
        _tenant = tenant;
        _actor = actor;
        _clock = clock;
    }

    public async Task<CashMovementDto> HandleAsync(Guid accountId, ManualCashMovementRequest request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        var account = await _repo.FindAccountByIdAsync(tenantId, accountId, ct)
            ?? throw new NotFoundException("La cuenta de caja no existe.");

        if (!account.IsActive)
            throw new BusinessRuleException("No se puede registrar un movimiento en una cuenta inactiva.");

        var reason = request.Direction == CashMovementDirection.In
            ? CashMovementReason.ManualIncome
            : CashMovementReason.ManualExpense;

        var movement = new CashMovement(
            tenantId,
            account.BranchId,
            accountId,
            request.Amount,
            request.Direction,
            reason,
            referenceType: null,
            referenceId: null,
            request.Note,
            _actor.ActorId,
            _clock.GetUtcNow());

        await _repo.AddMovementAsync(movement, ct);
        await _repo.SaveChangesAsync(ct);
        return MapToDto(movement);
    }

    internal static CashMovementDto MapToDto(CashMovement m) =>
        new(m.Id, m.AccountId, m.Amount, m.Direction, m.Reason, m.ReferenceType, m.ReferenceId, m.Note, m.CreatedAt);
}
