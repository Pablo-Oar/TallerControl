namespace TallerControl.Domain.Users;

public enum TenantRole
{
    Owner,
    AdminStaff,
    Mechanic,
    /// <summary>Solo lectura de facturación; no ocupa lugar del plan (E12).</summary>
    Accountant,
}
