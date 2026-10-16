using TallerControl.Domain.Common;

namespace TallerControl.Domain.Reminders;

/// <summary>
/// Recordatorio de mantenimiento/vencimiento para un vehículo. Al menos uno de DueDate o DueKm debe estar fijado.
/// </summary>
public sealed class Reminder : Entity, ITenantOwned, IVersioned, IAuditable, ISoftDeletable
{
    private Reminder()
    {
    }

    public Reminder(
        Guid tenantId,
        Guid vehicleId,
        ReminderKind kind,
        string title,
        DateTimeOffset? dueDate,
        int? dueKm,
        string? notes)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El recordatorio necesita un taller.", nameof(tenantId));

        if (vehicleId == Guid.Empty)
            throw new ArgumentException("El recordatorio necesita un vehículo.", nameof(vehicleId));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("El título es obligatorio.", nameof(title));

        if (!dueDate.HasValue && !dueKm.HasValue)
            throw new ArgumentException("El recordatorio debe tener una fecha límite o un km límite.");

        if (dueKm.HasValue && dueKm.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(dueKm), "El km no puede ser negativo.");

        TenantId = tenantId;
        VehicleId = vehicleId;
        Kind = kind;
        Title = title.Trim();
        DueDate = dueDate;
        DueKm = dueKm;
        Notes = notes?.Trim();
    }

    public Guid TenantId { get; private set; }
    public Guid VehicleId { get; private set; }
    public ReminderKind Kind { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public DateTimeOffset? DueDate { get; private set; }
    public int? DueKm { get; private set; }
    public DateTimeOffset? LastNotifiedAt { get; private set; }
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

    public void Update(ReminderKind kind, string title, DateTimeOffset? dueDate, int? dueKm, string? notes)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("El título es obligatorio.", nameof(title));

        if (!dueDate.HasValue && !dueKm.HasValue)
            throw new ArgumentException("El recordatorio debe tener una fecha límite o un km límite.");

        if (dueKm.HasValue && dueKm.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(dueKm), "El km no puede ser negativo.");

        Kind = kind;
        Title = title.Trim();
        DueDate = dueDate;
        DueKm = dueKm;
        Notes = notes?.Trim();
    }

    public void MarkNotified(DateTimeOffset at) => LastNotifiedAt = at;

    public void Delete(string reason, Guid? deletedBy, DateTimeOffset deletedAt)
    {
        if (IsDeleted) return;
        DeletedAt = deletedAt;
        DeletedBy = deletedBy;
        DeleteReason = reason?.Trim();
    }
}
