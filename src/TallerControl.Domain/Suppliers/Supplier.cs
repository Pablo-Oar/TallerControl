using TallerControl.Domain.Common;
using TallerControl.Domain.Customers;

namespace TallerControl.Domain.Suppliers;

public sealed class Supplier : Entity, ITenantOwned, IVersioned, IAuditable, ISoftDeletable
{
    public const int NameMaxLength = 200;
    public const int CuitMaxLength = 13;
    public const int PhoneMaxLength = 50;
    public const int EmailMaxLength = 200;
    public const int AddressMaxLength = 300;
    public const int NotesMaxLength = 1000;

    private Supplier()
    {
    }

    public Supplier(
        Guid tenantId,
        string name,
        string? cuit,
        VatCondition vatCondition,
        string? address,
        string? phone,
        string? email,
        string? notes)
    {
        TenantId = tenantId;
        Name = ValidateName(name);
        Cuit = NormalizeCuit(cuit);
        VatCondition = vatCondition;
        Address = address?.Trim();
        Phone = phone?.Trim();
        Email = Common.EmailFormat.NormalizeOptional(email);
        Notes = notes?.Trim();
    }

    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Cuit { get; private set; }
    public VatCondition VatCondition { get; private set; }
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Notes { get; private set; }
    public uint Version { get; private set; }

    // IAuditable
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
        string? cuit,
        VatCondition vatCondition,
        string? address,
        string? phone,
        string? email,
        string? notes)
    {
        Name = ValidateName(name);
        Cuit = NormalizeCuit(cuit);
        VatCondition = vatCondition;
        Address = address?.Trim();
        Phone = phone?.Trim();
        Email = Common.EmailFormat.NormalizeOptional(email);
        Notes = notes?.Trim();
    }

    public void SoftDelete(string? reason, Guid? deletedBy, DateTimeOffset deletedAt)
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

    private static string? NormalizeCuit(string? cuit)
    {
        if (string.IsNullOrWhiteSpace(cuit)) return null;
        var normalized = cuit.Trim().Replace("-", "").Replace(" ", "");
        if (normalized.Length != 11 || !normalized.All(char.IsDigit))
            throw new ArgumentException("El CUIT debe tener 11 dígitos.", nameof(cuit));
        return $"{normalized[..2]}-{normalized[2..10]}-{normalized[10]}";
    }
}
