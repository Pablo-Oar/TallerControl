using TallerControl.Domain.Common;

namespace TallerControl.Domain.Work;

/// <summary>
/// Archivo adjunto a una orden, presupuesto o vehículo. La FK al dueño es polimórfica
/// (owner_type + owner_id): la aplicación valida que exista y pertenezca al mismo taller
/// (doc 04 §3.4). El borrado lógico via deleted_at impide acceso pero el objeto en R2
/// se limpia a los 30 días por una tarea de fondo.
/// </summary>
public sealed class Attachment : Entity, ITenantOwned
{
    private static readonly IReadOnlySet<string> AllowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "application/pdf",
    };

    public const long MaxImageBytes = 10 * 1024 * 1024;    // 10 MB
    public const long MaxPdfBytes = 20 * 1024 * 1024;    // 20 MB
    public const int MaxPerDocument = 10;

    private Attachment()
    {
    }

    public Attachment(
        Guid tenantId,
        AttachmentOwnerType ownerType,
        Guid ownerId,
        string fileKey,
        string contentType,
        long sizeBytes,
        bool isInternal,
        Guid? createdBy,
        DateTimeOffset createdAt)
    {
        ValidateContentType(contentType);
        ValidateSize(contentType, sizeBytes);

        TenantId = tenantId;
        OwnerType = ownerType;
        OwnerId = ownerId;
        FileKey = fileKey;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        IsInternal = isInternal;
        Status = AttachmentStatus.PendingUpload;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public Guid TenantId { get; private set; }
    public AttachmentOwnerType OwnerType { get; private set; }
    public Guid OwnerId { get; private set; }
    public string FileKey { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public bool IsInternal { get; private set; }
    public AttachmentStatus Status { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt.HasValue;

    /// <summary>Marca el adjunto como listo después de confirmar la subida con magic bytes.</summary>
    public void Confirm() => Status = AttachmentStatus.Ready;

    /// <summary>Actualiza la visibilidad del adjunto.</summary>
    public void SetInternal(bool isInternal) => IsInternal = isInternal;

    /// <summary>Borrado lógico. El objeto en R2 se limpia a los 30 días por una tarea.</summary>
    public void SoftDelete(DateTimeOffset deletedAt) => DeletedAt = deletedAt;

    public static bool IsContentTypeAllowed(string contentType) =>
        AllowedContentTypes.Contains(contentType);

    public static long GetMaxBytes(string contentType) =>
        contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? MaxImageBytes : MaxPdfBytes;

    private static void ValidateContentType(string contentType)
    {
        if (!AllowedContentTypes.Contains(contentType))
            throw new ArgumentException(
                $"El tipo '{contentType}' no está permitido. Se aceptan: {string.Join(", ", AllowedContentTypes)}.",
                nameof(contentType));
    }

    private static void ValidateSize(string contentType, long sizeBytes)
    {
        var max = GetMaxBytes(contentType);
        if (sizeBytes <= 0 || sizeBytes > max)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes),
                $"El tamaño debe ser entre 1 B y {max / 1024 / 1024} MB.");
    }
}
