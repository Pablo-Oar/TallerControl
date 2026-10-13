using TallerControl.Domain.Common;

namespace TallerControl.Domain.Tenancy;

/// <summary>
/// Sucursal. Todo taller tiene al menos una, la principal; ninguna tabla asume <c>branch_id</c> nulo (D-15).
/// </summary>
public sealed class Branch : Entity, ITenantOwned, IVersioned
{
    public const string MainName = "Principal";

    private Branch()
    {
    }

    public Branch(Guid tenantId, string name, bool isMain = false)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("La sucursal necesita un taller.", nameof(tenantId));
        }

        TenantId = tenantId;
        Name = Tenant.ValidateName(name);
        IsMain = isMain;
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Address { get; private set; }

    public string? Phone { get; private set; }

    public string? Email { get; private set; }

    public bool IsMain { get; private set; }

    public uint Version { get; private set; }

    /// <summary>La sucursal "Principal" que se crea junto con el taller.</summary>
    public static Branch CreateMain(Guid tenantId) => new(tenantId, MainName, isMain: true);
}
