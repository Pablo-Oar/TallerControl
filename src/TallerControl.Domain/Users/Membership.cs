using TallerControl.Domain.Common;

namespace TallerControl.Domain.Users;

/// <summary>
/// Un usuario en un taller con un rol (doc 04 §3.1). Los permisos efectivos son los base del
/// rol más los finos adicionales. El dueño puede agregar permisos finos solo al mecánico.
/// </summary>
public sealed class Membership : Entity, ITenantOwned, IVersioned, IAudited
{
    private readonly List<string> _permissions = [];

    private Membership()
    {
    }

    public Membership(Guid tenantId, Guid userId, TenantRole role, IEnumerable<string>? finePermissions = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("La membresía necesita un taller.", nameof(tenantId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("La membresía necesita un usuario.", nameof(userId));
        }

        TenantId = tenantId;
        UserId = userId;
        Role = role;
        Status = MembershipStatus.Active;

        if (finePermissions is not null)
        {
            _permissions.AddRange(finePermissions);
        }
    }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public TenantRole Role { get; private set; }

    public IReadOnlyList<string> Permissions => _permissions;

    public MembershipStatus Status { get; private set; }

    public uint Version { get; private set; }

    /// <summary>Permisos efectivos: base del rol + finos adicionales.</summary>
    public IReadOnlySet<string> EffectivePermissions()
    {
        var effective = new HashSet<string>(Permission.BaseFor(Role));
        foreach (var p in _permissions)
        {
            effective.Add(p);
        }

        return effective;
    }

    public void ChangeRole(TenantRole newRole, IEnumerable<string>? finePermissions = null)
    {
        Role = newRole;
        _permissions.Clear();
        if (finePermissions is not null)
        {
            _permissions.AddRange(finePermissions);
        }
    }

    public void UpdatePermissions(IEnumerable<string> finePermissions)
    {
        _permissions.Clear();
        _permissions.AddRange(finePermissions);
    }

    public void Disable() => Status = MembershipStatus.Disabled;

    public void Enable() => Status = MembershipStatus.Active;
}
