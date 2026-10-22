using TallerControl.Application.Common;
using TallerControl.Application.Users;
using TallerControl.Domain.Users;

namespace TallerControl.Application.Work;

/// <summary>
/// Caso de uso: asignar mecánico a una OT (doc 05 §2.7, T-WO-12, REQ-12).
/// El mecánico solo puede autoasignarse si la orden no tiene mecánico.
/// Owner/Admin puede asignar a cualquier mecánico.
/// </summary>
public sealed class AssignMechanicHandler
{
    private readonly IWorkOrderRepository _repo;
    private readonly IMembershipRepository _memberships;
    private readonly IActorContext _actorContext;
    private readonly IOutbox _outbox;

    public AssignMechanicHandler(
        IWorkOrderRepository repo, IMembershipRepository memberships, IActorContext actorContext, IOutbox outbox)
    {
        _outbox = outbox;
        _repo = repo;
        _memberships = memberships;
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

        // El mecánico se asigna a sí mismo aunque no mande el id; dueño/administrativo deben elegir uno.
        Guid? requested = request.MechanicId ?? (isMechanic ? actorId : (Guid?)null);
        var mechanicId = requested
            ?? throw new BusinessRuleException("Indicá el mecánico a asignar.");

        // La FK del cuerpo se valida contra el taller (filtro global): debe ser un mecánico activo de este taller.
        var membership = await _memberships.FindByIdAsync(mechanicId, ct);
        if (membership is null || membership.Role != TenantRole.Mechanic || membership.Status != MembershipStatus.Active)
            throw new BusinessRuleException("El mecánico indicado no existe o no está activo.");

        try
        {
            order.Assign(mechanicId, isMechanic, actorId);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        // Si otro lo asigna (dueño o administrativo), el mecánico recibe un aviso en el celular.
        if (!isMechanic && mechanicId != actorId)
        {
            _outbox.Enqueue("push.work_order_assigned", new
            {
                membershipId = mechanicId,
                orderNumber = order.Number,
                workOrderId = id,
            });
        }

        await _repo.SaveChangesAsync(ct);
    }
}
