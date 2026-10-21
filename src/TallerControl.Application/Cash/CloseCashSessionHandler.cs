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

        // Con el efectivo contado, la nota deja asentado lo esperado y la diferencia (el saldo es la suma de movimientos).
        var closingNote = request.ClosingNote?.Trim();
        if (request.CountedAmount.HasValue)
        {
            var expected = await _repo.GetBalanceAsync(tenantId, accountId, ct);
            var difference = request.CountedAmount.Value - expected;
            var count = string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"Contado {request.CountedAmount.Value:0.00} · Esperado {expected:0.00} · Diferencia {difference:+0.00;-0.00;0.00}");
            closingNote = string.IsNullOrWhiteSpace(closingNote) ? count : $"{closingNote} | {count}";
        }

        try
        {
            session.Close(actorId, closingNote, _clock.GetUtcNow());
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        return OpenCashSessionHandler.MapToDto(session);
    }
}
