using TallerControl.Domain.Work;

namespace TallerControl.Application.Work;

/// <summary>Adjunto con URL de descarga firmada (doc 05 §2.8).</summary>
public sealed record AttachmentDto(
    Guid Id,
    AttachmentOwnerType OwnerType,
    Guid OwnerId,
    string ContentType,
    long SizeBytes,
    bool IsInternal,
    AttachmentStatus Status,
    string? DownloadUrl,
    Guid? CreatedBy,
    DateTimeOffset CreatedAt);

/// <summary>Cuerpo de POST /api/v1/attachments/upload-url (doc 05 §2.8).</summary>
public sealed record RequestAttachmentUploadUrlRequest(
    AttachmentOwnerType OwnerType,
    Guid OwnerId,
    string ContentType,
    long Size,
    bool IsInternal = false);

/// <summary>Respuesta de upload-url: ID del adjunto pendiente y URL PUT de 5 min.</summary>
public sealed record AttachmentUploadUrlResponse(Guid AttachmentId, string UploadUrl);

/// <summary>Cuerpo de PATCH /api/v1/attachments/{id} (cambiar is_internal).</summary>
public sealed record UpdateAttachmentRequest(bool IsInternal);
