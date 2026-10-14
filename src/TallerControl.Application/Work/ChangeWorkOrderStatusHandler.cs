using TallerControl.Application.Common;
using TallerControl.Domain.Work;

namespace TallerControl.Application.Work;

/// <summary>
/// Caso de uso: cambiar el estado de una OT (doc 05 §2.7, T-WO-06, T-WO-07, T-WO-08, T-WO-09, T-WO-10, REQ-12).
/// Delega la lógica de transición a <see cref="WorkOrder.CambiarEstado"/>.
/// </summary>
public sealed class ChangeWorkOrderStatusHandler
{
    private readonly IWorkOrderRepository _repo;
    private readonly IActorContext _actorContext;
    private readonly TimeProvider _clock;

    public ChangeWorkOrderStatusHandler(
        IWorkOrderRepository repo,
        IActorContext actorContext,
        TimeProvider clock)
    {
        _repo = repo;
        _actorContext = actorContext;
        _clock = clock;
    }

    public async Task<WorkOrderDto> HandleAsync(
        Guid id,
        ChangeStatusRequest request,
        bool isMechanic,
        bool canDeliver,
        bool canCancel,
        CancellationToken ct)
    {
        var order = await _repo.FindByIdAsync(id, ct)
            ?? throw new NotFoundException("La orden no existe.");

        var actorId = _actorContext.ActorId;
        var now = _clock.GetUtcNow();

        try
        {
            order.CambiarEstado(
                request.ToStatus,
                isMechanic: isMechanic,
                canDeliver: canDeliver,
                canCancel: canCancel,
                cancelReason: request.CancelReason,
                note: request.Note,
                changedBy: actorId,
                changedAt: now);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);

        return await _repo.GetFullAsync(id, includePrices: true, ct)
            ?? throw new InvalidOperationException("La orden actualizada no se pudo leer.");
    }
}
