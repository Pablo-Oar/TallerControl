using TallerControl.Domain.Common;

namespace TallerControl.Domain.Cash;

/// <summary>
/// Sesión de caja: corte del libro de movimientos de una cuenta (D-16).
/// El cierre actualiza el registro (no es append-only).
/// Solo puede haber una sesión abierta por cuenta a la vez.
/// </summary>
public sealed class CashSession : Entity, ITenantOwned, IAuditable
{
    public const int NoteMaxLength = 500;

    private CashSession()
    {
    }

    public CashSession(
        Guid tenantId,
        Guid branchId,
        Guid accountId,
        Guid openedByMembershipId,
        string? openingNote,
        DateTimeOffset openedAt)
    {
        TenantId = tenantId;
        BranchId = branchId;
        AccountId = accountId;
        OpenedByMembershipId = openedByMembershipId;
        OpeningNote = openingNote?.Trim();
        OpenedAt = openedAt;
        Status = CashSessionStatus.Open;
    }

    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid AccountId { get; private set; }
    public CashSessionStatus Status { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    public Guid OpenedByMembershipId { get; private set; }
    public string? OpeningNote { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public Guid? ClosedByMembershipId { get; private set; }
    public string? ClosingNote { get; private set; }

    // IAuditable
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public bool IsOpen => Status == CashSessionStatus.Open;

    public void Close(Guid closedByMembershipId, string? closingNote, DateTimeOffset closedAt)
    {
        if (Status != CashSessionStatus.Open)
            throw new InvalidOperationException("La sesión ya está cerrada.");

        ClosedByMembershipId = closedByMembershipId;
        ClosingNote = closingNote?.Trim();
        ClosedAt = closedAt;
        Status = CashSessionStatus.Closed;
    }
}
