using TallerControl.Domain.Common;

namespace TallerControl.Domain.Payments;

/// <summary>
/// Cobro sobre una orden de trabajo (REQ-15, doc 04 §3.5).
/// Se puede anular con motivo; nunca se borra físicamente.
/// </summary>
public sealed class Payment : Entity, ITenantOwned, IAudited, IAuditable
{
    public const int VoidReasonMaxLength = 500;
    public const int ReferenceMaxLength = 255;
    public const int IdempotencyKeyMaxLength = 128;

    private Payment()
    {
    }

    public Payment(
        Guid tenantId,
        Guid workOrderId,
        int number,
        PaymentMethod method,
        decimal amount,
        DateTimeOffset paidAt,
        string? reference,
        byte[] publicTokenHash,
        string? idempotencyKey,
        Guid? createdBy,
        DateTimeOffset createdAt)
    {
        TenantId = tenantId;
        WorkOrderId = workOrderId;
        Number = number;
        Method = method;
        Amount = ValidateAmount(amount);
        PaidAt = paidAt;
        Reference = reference?.Trim();
        Status = PaymentStatus.Valid;
        PublicTokenHash = publicTokenHash;
        IdempotencyKey = idempotencyKey?.Trim();
        CreatedAt = createdAt;
        CreatedBy = createdBy;
    }

    public Guid TenantId { get; private set; }
    public Guid WorkOrderId { get; private set; }
    public int Number { get; private set; }
    public PaymentMethod Method { get; private set; }
    public decimal Amount { get; private set; }
    public DateTimeOffset PaidAt { get; private set; }
    public string? Reference { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? VoidReason { get; private set; }
    public DateTimeOffset? VoidedAt { get; private set; }
    public Guid? VoidedBy { get; private set; }
    public byte[] PublicTokenHash { get; private set; } = [];

    /// <summary>Clave de idempotencia enviada por el cliente (opcional). Guardada 24 h (doc 05 §2.9).</summary>
    public string? IdempotencyKey { get; private set; }

    /// <summary>D-16: nulo hasta E11 (módulo de caja). FK a cash_accounts se agrega en esa etapa.</summary>
    public Guid? AccountId { get; private set; }

    // IAudited / IAuditable
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    /// <summary>Vincula el pago a una cuenta de caja (E11, D-16).</summary>
    public void SetAccountId(Guid accountId) => AccountId = accountId;

    /// <summary>Anula el pago. Lanza si ya está anulado.</summary>
    public void Anular(string reason, Guid? by, DateTimeOffset at)
    {
        if (Status == PaymentStatus.Voided)
            throw new InvalidOperationException("Este pago ya fue anulado.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("El motivo de anulación es obligatorio.", nameof(reason));

        Status = PaymentStatus.Voided;
        VoidReason = reason.Trim();
        VoidedAt = at;
        VoidedBy = by;
    }

    private static decimal ValidateAmount(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "El monto debe ser mayor a 0.");
        return amount;
    }
}
