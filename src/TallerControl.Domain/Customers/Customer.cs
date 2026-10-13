using TallerControl.Domain.Common;

namespace TallerControl.Domain.Customers;

/// <summary>
/// Dueño de un vehículo. Pertenece al taller; tiene borrado lógico con motivo (REQ-05, doc 04 §3.2).
/// </summary>
public sealed class Customer : Entity, ITenantOwned, IVersioned, IAudited, IAuditable, ISoftDeletable
{
    public const int NameMaxLength = 150;

    private Customer()
    {
    }

    public Customer(
        Guid tenantId,
        string name,
        string? email = null,
        string? phone = null,
        DocType? docType = null,
        string? docNumber = null,
        VatCondition vatCondition = VatCondition.ConsumidorFinal,
        string? address = null,
        bool doNotContact = false,
        string? notes = null)
    {
        TenantId = tenantId;
        Name = ValidateName(name);
        Email = email?.Trim().ToLowerInvariant();
        Phone = PhoneNormalizer.Normalize(phone);
        DocType = docType;
        DocNumber = NormalizeDocNumber(docNumber);
        VatCondition = vatCondition;
        Address = address?.Trim();
        DoNotContact = doNotContact;
        Notes = notes?.Trim();
    }

    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public DocType? DocType { get; private set; }
    public string? DocNumber { get; private set; }
    public VatCondition VatCondition { get; private set; }
    public string? Address { get; private set; }
    public bool DoNotContact { get; private set; }
    public byte[]? OwnerAccessCodeHash { get; private set; }
    public string? Notes { get; private set; }
    public uint Version { get; private set; }

    // IAuditable — los timestamps los setea AppDbContext en SaveChanges
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    // ISoftDeletable
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }
    public string? DeleteReason { get; private set; }

    public bool IsDeleted => DeletedAt.HasValue;

    public void Update(
        string name,
        string? email,
        string? phone,
        DocType? docType,
        string? docNumber,
        VatCondition vatCondition,
        string? address,
        bool doNotContact,
        string? notes)
    {
        Name = ValidateName(name);
        Email = email?.Trim().ToLowerInvariant();
        Phone = PhoneNormalizer.Normalize(phone);
        DocType = docType;
        DocNumber = NormalizeDocNumber(docNumber);
        VatCondition = vatCondition;
        Address = address?.Trim();
        DoNotContact = doNotContact;
        Notes = notes?.Trim();
    }

    public void Delete(string reason, Guid? deletedBy, DateTimeOffset deletedAt)
    {
        if (IsDeleted) return;
        DeletedAt = deletedAt;
        DeletedBy = deletedBy;
        DeleteReason = reason?.Trim();
    }

    public void Restore()
    {
        DeletedAt = null;
        DeletedBy = null;
        DeleteReason = null;
    }

    private static string ValidateName(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is < 1 or > NameMaxLength)
            throw new ArgumentException($"El nombre debe tener entre 1 y {NameMaxLength} caracteres.", nameof(name));
        return trimmed;
    }

    private static string? NormalizeDocNumber(string? docNumber) =>
        string.IsNullOrWhiteSpace(docNumber) ? null : docNumber.Trim();
}
