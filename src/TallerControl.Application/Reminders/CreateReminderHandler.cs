using TallerControl.Application.Common;
using TallerControl.Domain.Reminders;

namespace TallerControl.Application.Reminders;

/// <summary>Crea un recordatorio para un vehículo del taller (T-REM-01).</summary>
public sealed class CreateReminderHandler(IReminderRepository repo, ITenantContext tenant)
{
    public async Task<ReminderDto> HandleAsync(UpsertReminderRequest request, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        if (!await repo.VehicleExistsAsync(request.VehicleId, ct))
            throw new NotFoundException();

        Reminder reminder;
        try
        {
            reminder = new Reminder(tenantId, request.VehicleId, request.Kind, request.Title,
                request.DueDate, request.DueKm, request.Notes);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await repo.AddAsync(reminder, ct);
        await repo.SaveChangesAsync(ct);

        return await repo.GetFullAsync(reminder.Id, ct)
            ?? throw new InvalidOperationException("El recordatorio recién creado no se pudo leer.");
    }
}
