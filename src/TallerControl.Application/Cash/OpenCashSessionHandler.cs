using TallerControl.Application.Common;
using TallerControl.Domain.Cash;

namespace TallerControl.Application.Cash;

public sealed class OpenCashSessionHandler
{
    private readonly ICashRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly TimeProvider _clock;

    public OpenCashSessionHandler(ICashRepository repo, ITenantContext tenant, IActorContext actor, TimeProvider clock)
    {
        _repo = repo;
        _tenant = tenant;
        _actor = actor;
        _clock = clock;
    }

    public async Task<CashSessionDto> HandleAsync(Guid accountId, OpenCashSessionRequest request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        var account = await _repo.FindAccountByIdAsync(tenantId, accountId, ct)
            ?? throw new NotFoundException("La cuenta de caja no existe.");

        if (!account.IsActive)
            throw new BusinessRuleException("No se puede abrir una sesión en una cuenta inactiva.");

        var existing = await _repo.FindOpenSessionAsync(tenantId, accountId, ct);
        if (existing is not null)
            throw new ConflictException("Ya existe una sesión abierta para esta cuenta.");

        var actorId = _actor.ActorId
            ?? throw new InvalidOperationException("No hay usuario en la sesión.");

        var now = _clock.GetUtcNow();
        var session = new CashSession(tenantId, account.BranchId, accountId, actorId, request.OpeningNote, now);

        await _repo.AddSessionAsync(session, ct);
        await _repo.SaveChangesAsync(ct);
        return MapToDto(session);
    }

    internal static CashSessionDto MapToDto(CashSession s) =>
        new(s.Id, s.AccountId, s.Status, s.OpenedAt, s.OpenedByMembershipId,
            s.OpeningNote, s.ClosedAt, s.ClosedByMembershipId, s.ClosingNote);
}
