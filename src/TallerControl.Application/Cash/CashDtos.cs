using TallerControl.Domain.Cash;

namespace TallerControl.Application.Cash;

public sealed record CashAccountDto(
    Guid Id,
    Guid BranchId,
    string Name,
    CashAccountKind Kind,
    bool IsActive,
    uint Version
);

public sealed record CashSessionDto(
    Guid Id,
    Guid AccountId,
    CashSessionStatus Status,
    DateTimeOffset OpenedAt,
    Guid OpenedByMembershipId,
    string? OpeningNote,
    DateTimeOffset? ClosedAt,
    Guid? ClosedByMembershipId,
    string? ClosingNote
);

public sealed record CashMovementDto(
    Guid Id,
    Guid AccountId,
    decimal Amount,
    CashMovementDirection Direction,
    CashMovementReason Reason,
    string? ReferenceType,
    Guid? ReferenceId,
    string? Note,
    DateTimeOffset CreatedAt
);

public sealed record CreateCashAccountRequest(
    string Name,
    CashAccountKind Kind
);

public sealed record UpdateCashAccountRequest(
    string Name,
    CashAccountKind Kind,
    bool IsActive,
    uint Version
);

public sealed record OpenCashSessionRequest(
    string? OpeningNote
);

public sealed record CloseCashSessionRequest(
    string? ClosingNote
);

public sealed record ManualCashMovementRequest(
    decimal Amount,
    CashMovementDirection Direction,
    string Note
);
