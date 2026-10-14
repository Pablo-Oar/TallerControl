using TallerControl.Domain.Common;

namespace TallerControl.Domain.Qr;

// Desafío OTP para la verificación del dueño del vehículo.
// No implementa ISoftDeletable ni IAudited: es un registro temporal de sesión.
public sealed class OwnerVerification : ITenantOwned
{
    public const int MaxAttempts = 5;

    private OwnerVerification() { }

    public OwnerVerification(
        Guid tenantId,
        Guid qrCardId,
        Guid customerId,
        byte[] otpHash,
        DateTimeOffset expiresAt)
    {
        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        QrCardId = qrCardId;
        CustomerId = customerId;
        OtpHash = otpHash;
        ExpiresAt = expiresAt;
        Attempts = 0;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid QrCardId { get; private set; }
    public Guid CustomerId { get; private set; }
    public byte[] OtpHash { get; private set; } = [];
    public DateTimeOffset ExpiresAt { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
    public bool IsBlocked() => Attempts >= MaxAttempts;
    public bool IsConsumed() => ConsumedAt.HasValue;

    public void RecordAttempt()
    {
        if (IsConsumed())
            throw new InvalidOperationException("El desafío ya fue consumido.");
        Attempts++;
    }

    public void Consume(DateTimeOffset now)
    {
        if (IsConsumed())
            throw new InvalidOperationException("El desafío ya fue consumido.");
        ConsumedAt = now;
    }
}
