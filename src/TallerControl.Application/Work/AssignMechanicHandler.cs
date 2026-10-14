using TallerControl.Application.Common;

namespace TallerControl.Application.Work;

/// <summary>
/// Caso de uso: asignar mecánico a una OT (doc 05 §2.7, T-WO-12, REQ-12).
/// El mecánico solo puede autoasignarse si la orden no tiene mecánico.
/// Owner/Admin puede asignar a cualquier mecánico.
/// </summary>
public sealed class AssignMechanicHandler
{
    private readonly IWorkOrderRepository _repo;
    private readonly IActorContext _actorContext;

    public AssignMechanicHandler(IWorkOrderRepository repo, IActorContext actorContext)
    {
        _repo = repo;
        _actorContext = actorContext;
    }

    public async Task HandleAsync(Guid id, AssignMechanicRequest request, bool isMechanic, Guid? actorMembershipId, CancellationToken ct)
    {
        var order = await _repo.FindByIdAsync(id, ct)
            ?? throw new NotFoundException("La orden no existe.");

        // actorMembershipId se resuelve en el endpoint desde la sesión (membresía, no userId).
        var actorId = actorMembershipId
            ?? _actorContext.ActorId
            ?? throw new InvalidOperationException("No hay actor en la sesión.");

        try
        {
            order.Assign(request.MechanicId, isMechanic, actorId);
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
    }
}
