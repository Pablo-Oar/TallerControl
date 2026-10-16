using TallerControl.Domain.Common;

namespace TallerControl.Domain.Scheduling;

/// <summary>
/// Regla de disponibilidad semanal de un mecánico (qué días y en qué horario trabaja habitualmente).
/// </summary>
public sealed class AvailabilityRule : Entity, ITenantOwned, IAuditable
{
    private AvailabilityRule()
    {
    }

    public AvailabilityRule(
        Guid tenantId,
        Guid mechanicMembershipId,
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("La regla necesita un taller.", nameof(tenantId));

        if (mechanicMembershipId == Guid.Empty)
            throw new ArgumentException("La regla necesita un mecánico.", nameof(mechanicMembershipId));

        if (endTime <= startTime)
            throw new ArgumentException("La hora de fin debe ser posterior a la de inicio.", nameof(endTime));

        TenantId = tenantId;
        MechanicMembershipId = mechanicMembershipId;
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
    }

    public Guid TenantId { get; private set; }
    public Guid MechanicMembershipId { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }

    // IAuditable
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public void Update(DayOfWeek dayOfWeek, TimeOnly startTime, TimeOnly endTime)
    {
        if (endTime <= startTime)
            throw new ArgumentException("La hora de fin debe ser posterior a la de inicio.", nameof(endTime));

        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
    }
}
