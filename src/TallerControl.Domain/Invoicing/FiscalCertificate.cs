using TallerControl.Domain.Common;

namespace TallerControl.Domain.Invoicing;

/// <summary>
/// Certificado fiscal ARCA por CUIT (E12, doc 13 §8).
/// La clave privada se genera en el servidor, se guarda cifrada y nunca se expone.
/// Un taller admite hasta 3 CUIT con uno marcado como principal.
/// </summary>
public sealed class FiscalCertificate : Entity, ITenantOwned, IVersioned, IAuditable
{
    public const int CuitNormalizedLength = 13; // XX-XXXXXXXX-X
    public const int NameMaxLength = 200;
    public const int CsrMaxLength = 4000;
    public const int CertMaxLength = 4000;
    public const int MaxCuitsPerTenant = 3;

    private FiscalCertificate()
    {
    }

    /// <param name="privateKeyEncrypted">Clave privada RSA cifrada con Data Protection API. Nunca se expone.</param>
    /// <param name="csrPem">CSR en formato PEM para presentar en el portal ARCA.</param>
    public FiscalCertificate(
        Guid tenantId,
        string cuit,
        string name,
        bool isPrimary,
        byte[] privateKeyEncrypted,
        string csrPem)
    {
        TenantId = tenantId;
        Cuit = NormalizeCuit(cuit);
        Name = ValidateName(name);
        IsPrimary = isPrimary;
        PrivateKeyEncrypted = privateKeyEncrypted;
        CsrPem = csrPem.Trim();
        Status = FiscalCertificateStatus.Pending;
    }

    public Guid TenantId { get; private set; }
    public string Cuit { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsPrimary { get; private set; }

    /// <summary>Clave privada RSA cifrada. Solo lectura interna; nunca serializar.</summary>
    public byte[] PrivateKeyEncrypted { get; private set; } = [];
    public string CsrPem { get; private set; } = string.Empty;
    public string? CertificatePem { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public FiscalCertificateStatus Status { get; private set; }

    public uint Version { get; private set; }

    // IAuditable
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public bool IsActive => Status == FiscalCertificateStatus.Active
        && (ExpiresAt == null || ExpiresAt > DateTimeOffset.UtcNow);

    /// <summary>Carga el certificado firmado devuelto por ARCA y activa el certificado.</summary>
    public void MarkUploaded(string certificatePem, DateTimeOffset expiresAt)
    {
        if (Status == FiscalCertificateStatus.Revoked)
            throw new InvalidOperationException("No se puede activar un certificado revocado.");

        CertificatePem = certificatePem.Trim();
        ExpiresAt = expiresAt;
        Status = FiscalCertificateStatus.Active;
    }

    public void MarkExpired()
    {
        if (Status != FiscalCertificateStatus.Active)
            throw new InvalidOperationException("Solo se pueden marcar como expirados los certificados activos.");
        Status = FiscalCertificateStatus.Expired;
    }

    public void Revoke()
    {
        Status = FiscalCertificateStatus.Revoked;
    }

    public void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;

    public void UpdateName(string name) => Name = ValidateName(name);

    private static string NormalizeCuit(string cuit)
    {
        if (string.IsNullOrWhiteSpace(cuit))
            throw new ArgumentException("El CUIT es obligatorio.", nameof(cuit));

        var normalized = cuit.Trim().Replace("-", "").Replace(" ", "");
        if (normalized.Length != 11 || !normalized.All(char.IsDigit))
            throw new ArgumentException("El CUIT debe tener 11 dígitos.", nameof(cuit));

        return $"{normalized[..2]}-{normalized[2..10]}-{normalized[10]}";
    }

    private static string ValidateName(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is < 1 or > NameMaxLength)
            throw new ArgumentException($"El nombre debe tener entre 1 y {NameMaxLength} caracteres.", nameof(name));
        return trimmed;
    }
}
