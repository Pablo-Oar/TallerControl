using TallerControl.Domain.Common;

namespace TallerControl.Domain.Scheduling;

/// <summary>
/// Excepción de disponibilidad: un día específico bloqueado o con horario especial,
/// para un mecánico (MechanicMembershipId != null) o para todo el taller (null).
/// </summary>
public sealed class AvailabilityException : Entity, ITenantOwned, IAuditable
{
    private AvailabilityException()
    {
    }

    public AvailabilityException(
        Guid tenantId,
        Guid? mechanicMembershipId,
        DateOnly date,
        bool isBlocked,
        TimeOnly? startTime,
        TimeOnly? endTime,
        string? reason)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("La excepción necesita un taller.", nameof(tenantId));

        if (!isBlocked && (!startTime.HasValue || !endTime.HasValue))
            throw new ArgumentException("Una excepción de horario especial requiere hora de inicio y fin.");

        if (!isBlocked && startTime.HasValue && endTime.HasValue && endTime <= startTime)
            throw new ArgumentException("La hora de fin debe ser posterior a la de inicio.", nameof(endTime));

        TenantId = tenantId;
        MechanicMembershipId = mechanicMembershipId;
        Date = date;
        IsBlocked = isBlocked;
        StartTime = startTime;
        EndTime = endTime;
        Reason = reason?.Trim();
    }

    public Guid TenantId { get; private set; }

    /// <summary>Null = aplica a todo el taller; no-null = solo ese mecánico.</summary>
    public Guid? MechanicMembershipId { get; private set; }

    public DateOnly Date { get; private set; }

    /// <summary>True = día bloqueado; false = horario especial para ese día.</summary>
    public bool IsBlocked { get; private set; }

    public TimeOnly? StartTime { get; private set; }
    public TimeOnly? EndTime { get; private set; }
    public string? Reason { get; private set; }

    // IAuditable
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }
}
