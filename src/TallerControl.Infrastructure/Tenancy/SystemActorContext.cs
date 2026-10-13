using System.Net;
using TallerControl.Application.Common;
using TallerControl.Domain.Audit;

namespace TallerControl.Infrastructure.Tenancy;

/// <summary>
/// Actor por defecto: el sistema. E2 registra el actor basado en la sesión (usuario) y en las
/// peticiones públicas.
/// </summary>
internal sealed class SystemActorContext : IActorContext
{
    public AuditActorType ActorType => AuditActorType.System;

    public Guid? ActorId => null;

    public IPAddress? TruncatedIp => null;
}
