using TallerControl.Domain.Common;

namespace TallerControl.Domain.PublicLinks;

/// <summary>
/// Token de acceso público para presupuestos, órdenes y comprobantes de pago (doc 04 §3.6).
/// El token de 256 bits (32 bytes) vive solo en la URL; en la base se guarda el hash SHA-256.
/// </summary>
public sealed class PublicLink : Entity, ITenantOwned, IAuditable
{
    private PublicLink()
    {
    }

    public PublicLink(
        Guid tenantId,
        PublicLinkTargetType targetType,
        Guid targetId,
        byte[] tokenHash,
        DateTimeOffset? expiresAt)
    {
        TenantId = tenantId;
        TargetType = targetType;
        TargetId = targetId;
        TokenHash = tokenHash ?? throw new ArgumentNullException(nameof(tokenHash));
        ExpiresAt = expiresAt;
        AccessCount = 0;
    }

    public Guid TenantId { get; private set; }
    public PublicLinkTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public byte[] TokenHash { get; private set; } = [];
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? RevokedBy { get; private set; }
    public DateTimeOffset? LastAccessedAt { get; private set; }
    public int AccessCount { get; private set; }

    // IAuditable — AppDbContext setea CreatedAt/UpdatedAt
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public bool IsActive(DateTimeOffset now) =>
        RevokedAt == null && (ExpiresAt == null || ExpiresAt > now);

    public void Revoke(Guid? revokedBy, DateTimeOffset revokedAt)
    {
        if (RevokedAt.HasValue) return;
        RevokedAt = revokedAt;
        RevokedBy = revokedBy;
    }

    public void RecordAccess(DateTimeOffset accessedAt)
    {
        LastAccessedAt = accessedAt;
        AccessCount++;
    }
}
