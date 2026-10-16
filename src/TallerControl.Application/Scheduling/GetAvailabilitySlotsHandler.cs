using TallerControl.Application.Common;

namespace TallerControl.Application.Scheduling;

/// <summary>Calcula los slots libres para una fecha y duración requerida.</summary>
public sealed class GetAvailabilitySlotsHandler(ISchedulingRepository repo)
{
    public async Task<IReadOnlyList<AvailabilitySlotDto>> HandleAsync(
        DateOnly date, int requiredMinutes, CancellationToken ct)
    {
        if (requiredMinutes <= 0)
            throw new BusinessRuleException("La duración requerida debe ser mayor a cero.");

        return await repo.GetAvailabilitySlotsAsync(date, requiredMinutes, ct);
    }
}
