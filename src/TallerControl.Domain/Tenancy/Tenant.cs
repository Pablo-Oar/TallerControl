using TallerControl.Domain.Common;

namespace TallerControl.Domain.Tenancy;

/// <summary>Un taller (empresa). Nunca se borra: se suspende (doc 04 §3.1).</summary>
public sealed class Tenant : Entity, IVersioned, IAudited
{
    public const int NameMaxLength = 120;
    public const string DefaultTimeZone = "America/Argentina/Buenos_Aires";

    private Tenant()
    {
    }

    public Tenant(string name, DateTimeOffset createdAt, string? legalName = null, string? taxId = null)
    {
        Name = ValidateName(name);
        LegalName = legalName;
        TaxId = taxId;
        Status = TenantStatus.Active;
        TimeZone = DefaultTimeZone;
        CreatedAt = createdAt;
    }

    public string Name { get; private set; } = string.Empty;

    public string? LegalName { get; private set; }

    public string? TaxId { get; private set; }

    public TenantStatus Status { get; private set; }

    public string TimeZone { get; private set; } = DefaultTimeZone;

    public DateTimeOffset CreatedAt { get; private set; }

    public uint Version { get; private set; }

    public void Suspend() => Status = TenantStatus.Suspended;

    public void Activate() => Status = TenantStatus.Active;

    internal static string ValidateName(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is < 1 or > NameMaxLength)
        {
            throw new ArgumentException($"El nombre debe tener entre 1 y {NameMaxLength} caracteres.", nameof(name));
        }

        return trimmed;
    }
}
