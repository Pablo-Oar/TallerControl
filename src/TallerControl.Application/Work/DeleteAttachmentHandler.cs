using TallerControl.Application.Common;

namespace TallerControl.Application.Work;

/// <summary>
/// Caso de uso: borrar lógicamente un adjunto (doc 05 §2.7, T-ATT-06).
/// El objeto en R2 se limpia a los 30 días por una tarea de fondo.
/// </summary>
public sealed class DeleteAttachmentHandler
{
    private readonly IAttachmentRepository _repo;
    private readonly TimeProvider _clock;

    public DeleteAttachmentHandler(IAttachmentRepository repo, TimeProvider clock)
    {
        _repo = repo;
        _clock = clock;
    }

    public async Task HandleAsync(Guid id, CancellationToken ct)
    {
        var attachment = await _repo.FindByIdAsync(id, ct)
            ?? throw new NotFoundException("El adjunto no existe.");

        if (attachment.IsDeleted)
            return;

        attachment.SoftDelete(_clock.GetUtcNow());
        await _repo.SaveChangesAsync(ct);
    }
}
