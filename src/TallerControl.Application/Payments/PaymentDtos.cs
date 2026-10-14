using TallerControl.Domain.Payments;

namespace TallerControl.Application.Payments;

public sealed record PaymentDto(
    Guid Id,
    int Number,
    PaymentMethod Method,
    decimal Amount,
    DateTimeOffset PaidAt,
    string? Reference,
    PaymentStatus Status,
    string? VoidReason,
    DateTimeOffset? VoidedAt,
    string PublicToken
);

public sealed record WorkOrderPaymentsDto(
    IReadOnlyList<PaymentDto> Payments,
    decimal Total,
    decimal Paid,
    decimal Balance
);

public sealed record PublicPaymentDto(
    int Number,
    PaymentMethod Method,
    decimal Amount,
    DateTimeOffset PaidAt,
    string? Reference,
    PaymentStatus Status
);

public sealed record CreatePaymentRequest(
    PaymentMethod Method,
    decimal Amount,
    DateTimeOffset? PaidAt,
    string? Reference
);

public sealed record VoidPaymentRequest(
    string Reason
);
