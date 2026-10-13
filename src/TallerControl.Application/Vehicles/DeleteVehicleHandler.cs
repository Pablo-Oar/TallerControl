using TallerControl.Application.Common;

namespace TallerControl.Application.Vehicles;

/// <summary>
/// Caso de uso: borrado lógico del vehículo (doc 05 §2.3, T-VEH-09).
/// No se puede borrar si tiene órdenes abiertas (verificado en E4). Revoca la tarjeta QR activa (E8).
/// </summary>
public sealed class DeleteVehicleHandler
{
    private readonly IVehicleRepository _repo;
    private readonly IActorContext _actorContext;
    private readonly TimeProvider _clock;

    public DeleteVehicleHandler(IVehicleRepository repo, IActorContext actorContext, TimeProvider clock)
    {
        _repo = repo;
        _actorContext = actorContext;
        _clock = clock;
    }

    public async Task HandleAsync(Guid id, string reason, CancellationToken ct)
    {
        var vehicle = await _repo.FindByIdAsync(id, ct)
            ?? throw new NotFoundException();

        if (await _repo.HasOpenWorkOrdersAsync(id, ct))
            throw new BusinessRuleException("No se puede borrar un vehículo con órdenes de trabajo abiertas.");

        vehicle.Delete(reason, _actorContext.ActorId, _clock.GetUtcNow());
        await _repo.SaveChangesAsync(ct);
    }
}
