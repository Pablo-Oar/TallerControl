using TallerControl.Domain.Common;

namespace TallerControl.Domain.Cash;

/// <summary>
/// Movimiento de caja: solo inserción (append-only). La base revoca UPDATE para el rol de aplicación.
/// El saldo de una cuenta es la suma de sus movimientos (D-16).
/// </summary>
public sealed class CashMovement : Entity, ITenantOwned
{
    private CashMovement()
    {
    }

    public CashMovement(
        Guid tenantId,
        Guid branchId,
        Guid accountId,
        decimal amount,
        CashMovementDirection direction,
        CashMovementReason reason,
        string? referenceType,
        Guid? referenceId,
        string? note,
        Guid? createdByMembershipId,
        DateTimeOffset createdAt)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "El monto debe ser mayor que cero.");

        if (reason is CashMovementReason.ManualIncome or CashMovementReason.ManualExpense
            && string.IsNullOrWhiteSpace(note))
            throw new ArgumentException("La nota es obligatoria para movimientos manuales.", nameof(note));

        TenantId = tenantId;
        BranchId = branchId;
        AccountId = accountId;
        Amount = amount;
        Direction = direction;
        Reason = reason;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        Note = note?.Trim();
        CreatedByMembershipId = createdByMembershipId;
        CreatedAt = createdAt;
    }

    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid AccountId { get; private set; }
    public decimal Amount { get; private set; }
    public CashMovementDirection Direction { get; private set; }
    public CashMovementReason Reason { get; private set; }
    public string? ReferenceType { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public string? Note { get; private set; }
    public Guid? CreatedByMembershipId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
