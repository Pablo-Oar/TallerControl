using TallerControl.Application.Common;
using TallerControl.Domain.Work;

namespace TallerControl.Application.Work;

/// <summary>
/// Caso de uso: pedir URL prefirmada PUT para subir un adjunto (doc 05 §2.7, T-ATT-01, doc 07 §5).
/// Valida tipo/tamaño declarados. La clave es generada por el servidor, nunca llega del cliente.
/// La subida no queda confirmada hasta <see cref="ConfirmAttachmentHandler"/>.
/// </summary>
public sealed class RequestAttachmentUploadUrlHandler
{
    private static readonly TimeSpan PutExpiry = TimeSpan.FromMinutes(5);

    private readonly IWorkOrderRepository _woRepo;
    private readonly IAttachmentRepository _attachmentRepo;
    private readonly IObjectStorage _storage;
    private readonly ITenantContext _tenantContext;
    private readonly IActorContext _actorContext;
    private readonly TimeProvider _clock;

    public RequestAttachmentUploadUrlHandler(
        IWorkOrderRepository woRepo,
        IAttachmentRepository attachmentRepo,
        IObjectStorage storage,
        ITenantContext tenantContext,
        IActorContext actorContext,
        TimeProvider clock)
    {
        _woRepo = woRepo;
        _attachmentRepo = attachmentRepo;
        _storage = storage;
        _tenantContext = tenantContext;
        _actorContext = actorContext;
        _clock = clock;
    }

    public async Task<AttachmentUploadUrlResponse> HandleAsync(
        AttachmentOwnerType ownerType,
        Guid ownerId,
        RequestAttachmentUploadUrlRequest request,
        CancellationToken ct,
        Guid? mechanicMembershipId = null)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        // Verificar que el dueño exista y pertenezca al taller (T-ATT-01).
        if (ownerType == AttachmentOwnerType.WorkOrder)
        {
            // El mecánico solo sube fotos a sus órdenes o a las libres; una ajena es un 404 (REQ-14).
            _ = (mechanicMembershipId is { } mechanicId
                    ? await _woRepo.FindVisibleToMechanicAsync(ownerId, mechanicId, ct)
                    : await _woRepo.FindByIdAsync(ownerId, ct))
                ?? throw new NotFoundException("La orden no existe.");
        }

        if (!Attachment.IsContentTypeAllowed(request.ContentType))
            throw new BusinessRuleException(
                "Tipo de archivo no permitido.",
                new Dictionary<string, string[]> { ["contentType"] = ["Se aceptan JPEG, PNG, WebP y PDF."] });

        if (request.Size <= 0 || request.Size > Attachment.GetMaxBytes(request.ContentType))
            throw new BusinessRuleException(
                "El archivo supera el tamaño máximo.",
                new Dictionary<string, string[]> { ["size"] = [$"Máximo {Attachment.GetMaxBytes(request.ContentType) / 1024 / 1024} MB."] });

        var count = await _attachmentRepo.CountReadyAsync(ownerType, ownerId, ct);
        if (count >= Attachment.MaxPerDocument)
            throw new BusinessRuleException($"Máximo {Attachment.MaxPerDocument} adjuntos por documento.");

        var fileKey = $"tenants/{tenantId}/attachments/{Guid.CreateVersion7()}";
        var uploadUrl = _storage.CreatePresignedPutUrl(fileKey, request.ContentType, PutExpiry);

        var attachment = new Attachment(
            tenantId, ownerType, ownerId,
            fileKey, request.ContentType, request.Size,
            isInternal: request.IsInternal,
            createdBy: _actorContext.ActorId,
            createdAt: _clock.GetUtcNow());

        await _attachmentRepo.AddAsync(attachment, ct);
        await _attachmentRepo.SaveChangesAsync(ct);

        return new AttachmentUploadUrlResponse(attachment.Id, uploadUrl);
    }
}
