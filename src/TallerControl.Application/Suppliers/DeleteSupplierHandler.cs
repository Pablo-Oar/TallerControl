using TallerControl.Application.Common;

namespace TallerControl.Application.Suppliers;

public sealed class DeleteSupplierHandler
{
    private readonly ISupplierRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly TimeProvider _clock;

    public DeleteSupplierHandler(ISupplierRepository repo, ITenantContext tenant, IActorContext actor, TimeProvider clock)
    {
        _repo = repo;
        _tenant = tenant;
        _actor = actor;
        _clock = clock;
    }

    public async Task HandleAsync(Guid id, string? reason, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");
        var supplier = await _repo.FindByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException("El proveedor no existe.");

        supplier.SoftDelete(reason, _actor.ActorId, _clock.GetUtcNow());
        await _repo.SaveChangesAsync(ct);
    }
}
