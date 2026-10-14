using TallerControl.Application.Common;
using TallerControl.Domain.Work;

namespace TallerControl.Application.Work;

/// <summary>
/// Caso de uso: listar adjuntos de un documento (doc 05 §2.7, T-ATT-03).
/// Solo devuelve adjuntos en estado Ready y no borrados.
/// Para owner WorkOrder verifica que la orden pertenezca al taller de la sesión (aislamiento).
/// </summary>
public sealed class ListAttachmentsHandler
{
    private static readonly TimeSpan GetExpiry = TimeSpan.FromMinutes(10);

    private readonly IAttachmentRepository _repo;
    private readonly IObjectStorage _storage;
    private readonly IWorkOrderRepository _workOrderRepo;

    public ListAttachmentsHandler(IAttachmentRepository repo, IObjectStorage storage, IWorkOrderRepository workOrderRepo)
    {
        _repo = repo;
        _storage = storage;
        _workOrderRepo = workOrderRepo;
    }

    public async Task<IReadOnlyList<AttachmentDto>> HandleAsync(
        AttachmentOwnerType ownerType,
        Guid ownerId,
        CancellationToken ct)
    {
        // Verifica que el documento dueño exista en el tenant de la sesión (evita fugas cross-tenant).
        if (ownerType == AttachmentOwnerType.WorkOrder)
        {
            _ = await _workOrderRepo.FindByIdAsync(ownerId, ct)
                ?? throw new NotFoundException("La orden no existe.");
        }

        var attachments = await _repo.ListAsync(ownerType, ownerId, ct);
        return attachments
            .Select(a => AttachmentMapper.ToDtoWithUrl(a, _storage, GetExpiry))
            .ToList();
    }
}
