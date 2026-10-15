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

    /// <summary>Código de plan (D-17). En el MVP todos los talleres tienen "free".</summary>
    public const string DefaultPlanCode = "free";

    public string Name { get; private set; } = string.Empty;

    public string? LegalName { get; private set; }

    public string? TaxId { get; private set; }

    public string? Address { get; private set; }

    public string? Phone { get; private set; }

    public TenantStatus Status { get; private set; }

    public string TimeZone { get; private set; } = DefaultTimeZone;

    /// <summary>Plan comercial del taller (D-17). Punto de control preparado; no se cobra en el MVP.</summary>
    public string PlanCode { get; private set; } = DefaultPlanCode;

    public DateTimeOffset CreatedAt { get; private set; }

    public uint Version { get; private set; }

    public void Suspend() => Status = TenantStatus.Suspended;

    public void Activate() => Status = TenantStatus.Active;

    /// <summary>Actualiza los datos editables de REQ-04. El nombre es obligatorio; el resto, opcional.</summary>
    public void UpdateSettings(
        string name,
        string? legalName,
        string? taxId,
        string? address,
        string? phone,
        string timeZone)
    {
        Name = ValidateName(name);
        LegalName = legalName;
        TaxId = taxId;
        Address = address;
        Phone = phone;
        TimeZone = ValidateTimeZone(timeZone);
    }

    private static string ValidateTimeZone(string timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZone))
        {
            throw new ArgumentException("La zona horaria es obligatoria.", nameof(timeZone));
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            throw new ArgumentException($"Zona horaria desconocida: {timeZone}.", nameof(timeZone));
        }
        catch (InvalidTimeZoneException)
        {
            throw new ArgumentException($"Zona horaria inválida: {timeZone}.", nameof(timeZone));
        }

        return timeZone;
    }

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
