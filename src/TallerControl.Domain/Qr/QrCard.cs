using TallerControl.Domain.Common;

namespace TallerControl.Domain.Qr;

public sealed class QrCard : Entity, ITenantOwned, IVersioned, IAudited, IAuditable
{
    private QrCard() { }

    public QrCard(
        Guid tenantId,
        string code,
        Guid batchId,
        DateTimeOffset createdAt)
    {
        TenantId = tenantId;
        Code = code;
        BatchId = batchId;
        Status = QrCardStatus.Free;
        CreatedAt = createdAt;
    }

    public Guid TenantId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public Guid BatchId { get; private set; }
    public QrCardStatus Status { get; private set; }
    public Guid? VehicleId { get; private set; }
    public DateTimeOffset? AssignedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevokeReason { get; private set; }
    public uint Version { get; private set; }

    // IAuditable
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public void Assign(Guid vehicleId, DateTimeOffset now)
    {
        if (Status != QrCardStatus.Free)
            throw new InvalidOperationException($"Solo se puede asignar una tarjeta libre. Estado actual: {Status}.");

        Status = QrCardStatus.Assigned;
        VehicleId = vehicleId;
        AssignedAt = now;
    }

    public void Unassign()
    {
        if (Status is not (QrCardStatus.Assigned or QrCardStatus.Lost))
            throw new InvalidOperationException($"Solo se puede desasignar una tarjeta asignada o perdida. Estado actual: {Status}.");

        Status = QrCardStatus.Free;
        VehicleId = null;
        AssignedAt = null;
    }

    public void MarkLost()
    {
        if (Status != QrCardStatus.Assigned)
            throw new InvalidOperationException("Solo se puede marcar como perdida una tarjeta asignada.");

        Status = QrCardStatus.Lost;
    }

    public void UnmarkLost()
    {
        if (Status != QrCardStatus.Lost)
            throw new InvalidOperationException("La tarjeta no está marcada como perdida.");

        Status = QrCardStatus.Assigned;
    }

    public void Revoke(string reason, DateTimeOffset now)
    {
        if (Status == QrCardStatus.Revoked)
            throw new InvalidOperationException("La tarjeta ya está revocada.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Se requiere un motivo para revocar la tarjeta.", nameof(reason));

        Status = QrCardStatus.Revoked;
        RevokeReason = reason.Trim();
        RevokedAt = now;
        VehicleId = null;
    }
}
