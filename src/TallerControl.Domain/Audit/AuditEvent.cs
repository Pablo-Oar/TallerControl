using System.Net;
using TallerControl.Domain.Common;

namespace TallerControl.Domain.Audit;

/// <summary>
/// Registro de auditoría: quién cambió qué y cuándo (REQ-19). Solo se inserta; la base lo impide
/// también para el dueño con un trigger (doc 04 §3.7).
/// </summary>
public sealed class AuditEvent : Entity, IOptionalTenantOwned
{
    private AuditEvent()
    {
    }

    public AuditEvent(
        Guid? tenantId,
        AuditActorType actorType,
        Guid? actorId,
        string entityType,
        Guid entityId,
        string action,
        string changesJson,
        IPAddress? ipTruncated,
        DateTimeOffset occurredAt)
    {
        TenantId = tenantId;
        ActorType = actorType;
        ActorId = actorId;
        EntityType = entityType;
        EntityId = entityId;
        Action = action;
        ChangesJson = changesJson;
        IpTruncated = ipTruncated;
        OccurredAt = occurredAt;
    }

    public Guid? TenantId { get; private set; }

    public AuditActorType ActorType { get; private set; }

    public Guid? ActorId { get; private set; }

    public string EntityType { get; private set; } = string.Empty;

    public Guid EntityId { get; private set; }

    /// <summary>Por ejemplo <c>branch.updated</c> o <c>work_order.status_changed</c>.</summary>
    public string Action { get; private set; } = string.Empty;

    /// <summary>JSON <c>{campo: [antes, después]}</c>, sin datos sensibles.</summary>
    public string ChangesJson { get; private set; } = "{}";

    public IPAddress? IpTruncated { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}
