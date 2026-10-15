using TallerControl.Application.Common;

namespace TallerControl.Application.Cash;

public sealed class DeleteCashAccountHandler
{
    private readonly ICashRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly TimeProvider _clock;

    public DeleteCashAccountHandler(ICashRepository repo, ITenantContext tenant, IActorContext actor, TimeProvider clock)
    {
        _repo = repo;
        _tenant = tenant;
        _actor = actor;
        _clock = clock;
    }

    public async Task HandleAsync(Guid id, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");
        var account = await _repo.FindAccountByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException("La cuenta de caja no existe.");

        var openSession = await _repo.FindOpenSessionAsync(tenantId, id, ct);
        if (openSession is not null)
            throw new BusinessRuleException("No se puede eliminar una cuenta con una sesión abierta.");

        account.SoftDelete(null, _actor.ActorId, _clock.GetUtcNow());
        await _repo.SaveChangesAsync(ct);
    }
}
