using TallerControl.Domain.Common;

namespace TallerControl.Domain.Qr;

// Append-only: REVOKE UPDATE/DELETE aplicado en la migración.
// TenantId puede ser null si el código escaneado no existe en ningún taller.
public sealed class QrScanEvent : IOptionalTenantOwned
{
    private QrScanEvent() { }

    public QrScanEvent(
        Guid? tenantId,
        Guid? qrCardId,
        byte[] codeHash,
        QrScanEventType eventType,
        string? outcome,
        string? ipTruncated,
        string? userAgent,
        DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        QrCardId = qrCardId;
        CodeHash = codeHash;
        EventType = eventType;
        Outcome = outcome;
        IpTruncated = ipTruncated;
        UserAgent = userAgent;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid? QrCardId { get; private set; }
    public byte[] CodeHash { get; private set; } = [];
    public QrScanEventType EventType { get; private set; }
    public string? Outcome { get; private set; }
    public string? IpTruncated { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
