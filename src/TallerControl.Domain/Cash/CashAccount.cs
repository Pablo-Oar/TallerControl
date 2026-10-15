using TallerControl.Domain.Common;

namespace TallerControl.Domain.Cash;

public sealed class CashAccount : Entity, ITenantOwned, IVersioned, IAuditable, ISoftDeletable
{
    public const int NameMaxLength = 100;

    private CashAccount()
    {
    }

    public CashAccount(Guid tenantId, Guid branchId, string name, CashAccountKind kind)
    {
        TenantId = tenantId;
        BranchId = branchId;
        Name = ValidateName(name);
        Kind = kind;
        IsActive = true;
    }

    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public CashAccountKind Kind { get; private set; }
    public bool IsActive { get; private set; }
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

    public void Update(string name, CashAccountKind kind, bool isActive)
    {
        Name = ValidateName(name);
        Kind = kind;
        IsActive = isActive;
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
}
