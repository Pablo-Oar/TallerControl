using TallerControl.Application.Common;

namespace TallerControl.Application.Scheduling;

/// <summary>
/// Actualiza la fecha, mecánico o notas de un turno (concurrencia xmin/If-Match). Si queda con mecánico, el servidor
/// valida su horario y que no tenga otro turno encima (A-05); el Dueño puede forzarlo.
/// </summary>
public sealed class UpdateAppointmentHandler(ISchedulingRepository repo)
{
    public async Task<AppointmentDto> HandleAsync(
        Guid id, UpdateAppointmentRequest request, uint version, bool canForce, CancellationToken ct)
    {
        var appointment = await repo.FindByIdAsync(id, ct)
            ?? throw new NotFoundException();

        if (appointment.Version != version)
            throw new ConcurrencyException();

        if (request.MechanicMembershipId.HasValue
            && !await repo.MembershipExistsAsync(request.MechanicMembershipId.Value, ct))
            throw new NotFoundException("El mecánico no existe.");

        if (request.Force && !canForce)
            throw new BusinessRuleException("Solo el Dueño puede agendar un turno fuera de horario o encimado.");

        if (request.MechanicMembershipId.HasValue && !request.Force)
        {
            var conflict = await repo.CheckSlotAsync(
                request.MechanicMembershipId.Value, request.ScheduledAt, appointment.TotalDurationMinutes, id, ct);
            if (conflict is not null)
            {
                var next = await repo.FindNextFreeSlotAsync(
                    request.MechanicMembershipId, request.ScheduledAt, appointment.TotalDurationMinutes, ct);
                var message = conflict.Message;
                if (next is not null)
                {
                    var zone = TimeZoneInfo.FindSystemTimeZoneById(await repo.GetTenantTimeZoneIdAsync(ct));
                    message += $" Próximo hueco libre: {TimeZoneInfo.ConvertTime(next.Start, zone):dddd dd/MM HH:mm}.";
                }

                throw new BusinessRuleException(message);
            }
        }

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
