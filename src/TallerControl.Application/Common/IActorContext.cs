using System.Net;
using TallerControl.Domain.Audit;

namespace TallerControl.Application.Common;

/// <summary>
/// Quién origina el cambio actual, para la auditoría. Sin sesión ni petición pública el actor es
/// el sistema. <see cref="TruncatedIp"/> llega ya truncada (nunca la IP completa).
/// </summary>
public interface IActorContext
{
    AuditActorType ActorType { get; }

    Guid? ActorId { get; }

    IPAddress? TruncatedIp { get; }
}
