using TallerControl.Application.Common;

namespace TallerControl.Application.CounterSales;

public sealed class DeleteCounterSaleHandler
{
    private readonly ICounterSaleRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly TimeProvider _clock;

    public DeleteCounterSaleHandler(ICounterSaleRepository repo, ITenantContext tenant, IActorContext actor, TimeProvider clock)
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
        var sale = await _repo.FindByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException("La venta no existe.");

        if (sale.Status == Domain.CounterSales.CounterSaleStatus.Confirmed)
            throw new BusinessRuleException("No se puede eliminar una venta confirmada.");

        sale.SoftDelete(null, _actor.ActorId, _clock.GetUtcNow());
        await _repo.SaveChangesAsync(ct);
    }
}
