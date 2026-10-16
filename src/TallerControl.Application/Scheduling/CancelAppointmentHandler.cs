using TallerControl.Application.Common;

namespace TallerControl.Application.Scheduling;

/// <summary>Cancela un turno con motivo obligatorio.</summary>
public sealed class CancelAppointmentHandler(ISchedulingRepository repo)
{
    public async Task HandleAsync(Guid id, CancelAppointmentRequest request, CancellationToken ct)
    {
        var appointment = await repo.FindByIdAsync(id, ct)
            ?? throw new NotFoundException();

        try
        {
            appointment.Cancel(request.Reason);
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
    }
}
