using TallerControl.Application.Common;

namespace TallerControl.Application.Cash;

public sealed class CloseCashSessionHandler
{
    private readonly ICashRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly TimeProvider _clock;

    public CloseCashSessionHandler(ICashRepository repo, ITenantContext tenant, IActorContext actor, TimeProvider clock)
    {
        _repo = repo;
        _tenant = tenant;
        _actor = actor;
        _clock = clock;
    }

    public async Task<CashSessionDto> HandleAsync(Guid accountId, CloseCashSessionRequest request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        var session = await _repo.FindOpenSessionAsync(tenantId, accountId, ct)
            ?? throw new NotFoundException("No hay una sesión abierta para esta cuenta.");

        var actorId = _actor.ActorId
            ?? throw new InvalidOperationException("No hay usuario en la sesión.");

        try
        {
            session.Close(actorId, request.ClosingNote, _clock.GetUtcNow());
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        return OpenCashSessionHandler.MapToDto(session);
    }
}
