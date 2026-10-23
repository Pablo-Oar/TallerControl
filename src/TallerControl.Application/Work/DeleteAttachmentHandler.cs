using TallerControl.Application.Common;
using TallerControl.Domain.Work;

namespace TallerControl.Application.Work;

/// <summary>
/// Caso de uso: borrar lógicamente un adjunto (doc 05 §2.7, T-ATT-06).
/// El objeto en R2 se limpia a los 30 días por una tarea de fondo.
/// </summary>
public sealed class DeleteAttachmentHandler
{
    private readonly IAttachmentRepository _repo;
    private readonly IWorkOrderRepository _woRepo;
    private readonly TimeProvider _clock;

    public DeleteAttachmentHandler(IAttachmentRepository repo, IWorkOrderRepository woRepo, TimeProvider clock)
    {
        _repo = repo;
        _woRepo = woRepo;
        _clock = clock;
    }

    /// <param name="mechanicMembershipId">Si es un mecánico: solo puede quitar fotos de las órdenes que tiene asignadas.</param>
    public async Task HandleAsync(Guid id, CancellationToken ct, Guid? mechanicMembershipId = null)
    {
        var attachment = await _repo.FindByIdAsync(id, ct)
            ?? throw new NotFoundException("El adjunto no existe.");

        if (mechanicMembershipId is { } mechanicId)
        {
            var order = attachment.OwnerType == AttachmentOwnerType.WorkOrder
                ? await _woRepo.FindByIdAsync(attachment.OwnerId, ct)
                : null;
            if (order is null || order.AssignedMechanicId != mechanicId)
                throw new UnauthorizedAccessException("El mecánico solo puede quitar fotos de sus propias órdenes.");
        }

        if (attachment.IsDeleted)
            return;

        attachment.SoftDelete(_clock.GetUtcNow());
        await _repo.SaveChangesAsync(ct);
    }
}
