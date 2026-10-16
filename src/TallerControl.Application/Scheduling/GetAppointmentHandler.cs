using TallerControl.Application.Common;

namespace TallerControl.Application.Scheduling;

/// <summary>Devuelve el detalle de un turno.</summary>
public sealed class GetAppointmentHandler(ISchedulingRepository repo)
{
    public async Task<AppointmentDto> HandleAsync(Guid id, CancellationToken ct)
    {
        return await repo.GetFullAsync(id, ct)
            ?? throw new NotFoundException();
    }
}
