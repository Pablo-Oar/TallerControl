using TallerControl.Application.Common;

namespace TallerControl.Application.Scheduling;

/// <summary>Actualiza la fecha, mecánico o notas de un turno (concurrencia xmin/If-Match).</summary>
public sealed class UpdateAppointmentHandler(ISchedulingRepository repo)
{
    public async Task<AppointmentDto> HandleAsync(Guid id, UpdateAppointmentRequest request, uint version, CancellationToken ct)
    {
        var appointment = await repo.FindByIdAsync(id, ct)
            ?? throw new NotFoundException();

        if (appointment.Version != version)
            throw new ConcurrencyException();

        if (request.MechanicMembershipId.HasValue
            && !await repo.MembershipExistsAsync(request.MechanicMembershipId.Value, ct))
            throw new NotFoundException("El mecánico no existe.");

        try
        {
            appointment.Update(request.ScheduledAt, request.MechanicMembershipId, request.Notes);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await repo.SaveChangesAsync(ct);

        return await repo.GetFullAsync(id, ct)
            ?? throw new InvalidOperationException("El turno actualizado no se pudo leer.");
    }
}
