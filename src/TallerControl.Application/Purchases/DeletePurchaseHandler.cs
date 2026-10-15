using TallerControl.Application.Common;

namespace TallerControl.Application.Purchases;

public sealed class DeletePurchaseHandler
{
    private readonly IPurchaseRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly TimeProvider _clock;

    public DeletePurchaseHandler(IPurchaseRepository repo, ITenantContext tenant, IActorContext actor, TimeProvider clock)
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
        var purchase = await _repo.FindByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException("La compra no existe.");

        if (purchase.Status == Domain.Purchases.PurchaseStatus.Confirmed)
            throw new BusinessRuleException("No se puede eliminar una compra confirmada.");

        purchase.SoftDelete(null, _actor.ActorId, _clock.GetUtcNow());
        await _repo.SaveChangesAsync(ct);
    }
}
