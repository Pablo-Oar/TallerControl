using TallerControl.Domain.Common;

namespace TallerControl.Domain.Invoicing;

/// <summary>
/// Punto de venta Web Services de ARCA (E12, doc 13 §8).
/// Pertenece a un certificado fiscal del taller.
/// </summary>
public sealed class FiscalPointOfSale : Entity, ITenantOwned, IVersioned, IAuditable
{
    public const int NameMaxLength = 200;

    private FiscalPointOfSale()
    {
    }

    public FiscalPointOfSale(
        Guid tenantId,
        Guid fiscalCertificateId,
        int number,
        string name)
    {
        TenantId = tenantId;
        FiscalCertificateId = fiscalCertificateId;
        Number = ValidateNumber(number);
        Name = ValidateName(name);
        IsActive = true;
    }

    public Guid TenantId { get; private set; }
    public Guid FiscalCertificateId { get; private set; }

    /// <summary>Número del punto de venta en ARCA (entero positivo, único por taller).</summary>
    public int Number { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public uint Version { get; private set; }

    // IAuditable
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    // Navigation
    public FiscalCertificate FiscalCertificate { get; private set; } = null!;

    public void Update(string name) => Name = ValidateName(name);

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    private static int ValidateNumber(int number)
    {
        if (number <= 0)
            throw new ArgumentOutOfRangeException(nameof(number), "El número de punto de venta debe ser mayor a 0.");
        return number;
    }

    private static string ValidateName(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is < 1 or > NameMaxLength)
            throw new ArgumentException($"El nombre debe tener entre 1 y {NameMaxLength} caracteres.", nameof(name));
        return trimmed;
    }
}
