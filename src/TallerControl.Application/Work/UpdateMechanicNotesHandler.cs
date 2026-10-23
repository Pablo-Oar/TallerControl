using TallerControl.Application.Common;

namespace TallerControl.Application.Work;

/// <summary>
/// Caso de uso: mecánico actualiza sus notas en la OT (doc 05 §2.7, T-WO-13, REQ-14).
/// Solo el mecánico asignado puede actualizar sus notas.
/// </summary>
public sealed class UpdateMechanicNotesHandler
{
    private readonly IWorkOrderRepository _repo;
    private readonly IActorContext _actorContext;

    public UpdateMechanicNotesHandler(IWorkOrderRepository repo, IActorContext actorContext)
    {
        _repo = repo;
        _actorContext = actorContext;
    }

    public async Task HandleAsync(Guid id, UpdateMechanicNotesRequest request, Guid mechanicMembershipId, CancellationToken ct)
    {
        var order = await _repo.FindByIdAsync(id, ct)
            ?? throw new NotFoundException("La orden no existe.");

        // Solo el mecánico asignado puede actualizar sus notas (T-WO-13, REQ-14).
        if (order.AssignedMechanicId != mechanicMembershipId)
            throw new UnauthorizedAccessException("Solo el mecánico asignado puede actualizar las notas del trabajo.");

        try
        {
            order.UpdateMechanicNotes(request.MechanicNotes);
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
