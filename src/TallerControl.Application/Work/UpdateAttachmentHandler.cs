using TallerControl.Application.Common;

namespace TallerControl.Application.Work;

/// <summary>
/// Caso de uso: actualizar visibilidad de un adjunto (doc 05 §2.7, T-ATT-05).
/// </summary>
public sealed class UpdateAttachmentHandler
{
    private readonly IAttachmentRepository _repo;

    public UpdateAttachmentHandler(IAttachmentRepository repo) => _repo = repo;

    public async Task<AttachmentDto> HandleAsync(Guid id, UpdateAttachmentRequest request, CancellationToken ct)
    {
        var attachment = await _repo.FindByIdAsync(id, ct)
            ?? throw new NotFoundException("El adjunto no existe.");

        if (attachment.IsDeleted)
            throw new NotFoundException("El adjunto no existe.");

        attachment.SetInternal(request.IsInternal);
        await _repo.SaveChangesAsync(ct);

        return AttachmentMapper.ToDto(attachment);
    }
}
