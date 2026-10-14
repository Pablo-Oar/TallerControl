using TallerControl.Application.Common;
using TallerControl.Domain.Work;

namespace TallerControl.Application.Work;

/// <summary>
/// Caso de uso: confirmar que el adjunto se subió (doc 03 §flujo de adjuntos, T-ATT-02, doc 07 §5).
/// Valida HEAD + magic bytes antes de marcar el adjunto como Ready.
/// </summary>
public sealed class ConfirmAttachmentHandler(IAttachmentRepository repo, IObjectStorage storage)
{
    private readonly IAttachmentRepository _repo = repo;
    private readonly IObjectStorage _storage = storage;

    public async Task<AttachmentDto> HandleAsync(Guid id, CancellationToken ct)
    {
        var attachment = await _repo.FindByIdAsync(id, ct)
            ?? throw new NotFoundException("El adjunto no existe.");

        if (attachment.Status == AttachmentStatus.Ready)
            return AttachmentMapper.ToDtoWithUrl(attachment, _storage, TimeSpan.FromMinutes(10));

        var stored = await _storage.HeadAsync(attachment.FileKey, ct)
            ?? throw new BusinessRuleException(
                "No se encontró el archivo subido.",
                new Dictionary<string, string[]> { ["attachment"] = ["Pedí una URL de subida y volvé a intentar."] });

        if (stored.Size != attachment.SizeBytes || stored.ContentType is null
            || !string.Equals(stored.ContentType, attachment.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                "El objeto subido no coincide con el tipo o tamaño declarado.",
                new Dictionary<string, string[]> { ["attachment"] = ["Tipo o tamaño distintos a los declarados."] });
        }

        var prefix = await _storage.ReadPrefixAsync(attachment.FileKey, AttachmentMagicBytes.Length, ct);
        if (!AttachmentMagicBytes.IsValid(attachment.ContentType, prefix))
        {
            throw new BusinessRuleException(
                "El contenido del archivo no corresponde al tipo declarado.",
                new Dictionary<string, string[]> { ["attachment"] = ["Los bytes no coinciden con el tipo declarado."] });
        }

        attachment.Confirm();
        await _repo.SaveChangesAsync(ct);

        return AttachmentMapper.ToDtoWithUrl(attachment, _storage, TimeSpan.FromMinutes(10));
    }
}
