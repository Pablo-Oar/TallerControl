using TallerControl.Application.Common;
using TallerControl.Domain.Scheduling;

namespace TallerControl.Application.Scheduling;

/// <summary>Lista paginada de turnos con filtros opcionales.</summary>
public sealed class GetAppointmentsHandler(ISchedulingRepository repo)
{
    public Task<Page<AppointmentListDto>> HandleAsync(
        DateOnly? date, Guid? mechanicMembershipId, string? statusStr,
        int page, int pageSize, CancellationToken ct)
    {
        AppointmentStatus? status = null;
        if (!string.IsNullOrEmpty(statusStr) && Enum.TryParse<AppointmentStatus>(statusStr, ignoreCase: true, out var parsed))
            status = parsed;

        return repo.SearchAsync(date, mechanicMembershipId, status, page, pageSize, ct);
    }
}
