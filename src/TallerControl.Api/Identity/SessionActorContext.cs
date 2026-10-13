using System.Net;
using TallerControl.Application.Common;
using TallerControl.Domain.Audit;
using TallerControl.Infrastructure.Identity;

namespace TallerControl.Api.Identity;

/// <summary>
/// Actor para la auditoría: usuario autenticado o, si no hay sesión, el sistema. La IP se guarda
/// truncada (doc 07 §6): /24 en IPv4 y /64 en IPv6.
/// </summary>
internal sealed class SessionActorContext : IActorContext
{
    private readonly IHttpContextAccessor _accessor;

    public SessionActorContext(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public AuditActorType ActorType => ActorId is null ? AuditActorType.System : AuditActorType.User;

    public Guid? ActorId
    {
        get
        {
            var raw = _accessor.HttpContext?.User.FindFirst(SessionClaims.UserId)?.Value;
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public IPAddress? TruncatedIp
    {
        get
        {
            var ip = _accessor.HttpContext?.Connection.RemoteIpAddress;
            return ip is null ? null : TruncateIpAddress(ip);
        }
    }

    internal static IPAddress TruncateIpAddress(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        if (bytes.Length == 4)
        {
            // IPv4 /24: ceros en el último octeto.
            bytes[3] = 0;
        }
        else if (bytes.Length == 16)
        {
            // IPv6 /64: ceros en los últimos 8 octetos.
            for (var i = 8; i < 16; i++)
            {
                bytes[i] = 0;
            }
        }

        return new IPAddress(bytes);
    }
}
