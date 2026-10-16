using TallerControl.Application.Common;

namespace TallerControl.Application.Reminders;

public sealed class UpdateReminderHandler(IReminderRepository repo)
{
    public async Task<ReminderDto> HandleAsync(
        Guid id, uint ifMatchVersion, UpsertReminderRequest request, CancellationToken ct)
    {
        var reminder = await repo.FindByIdAsync(id, ct) ?? throw new NotFoundException();

        if (reminder.Version != ifMatchVersion)
            throw new ConcurrencyException();

        try
        {
            reminder.Update(request.Kind, request.Title, request.DueDate, request.DueKm, request.Notes);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await repo.SaveChangesAsync(ct);

        return await repo.GetFullAsync(id, ct)
            ?? throw new InvalidOperationException("El recordatorio no se pudo leer después de la edición.");
    }
}
