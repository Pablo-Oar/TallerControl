using TallerControl.Application.Common;

namespace TallerControl.Application.Reminders;

/// <summary>Da por cumplido un recordatorio (o lo vuelve a pendiente). Un cumplido deja de avisar.</summary>
public sealed class CompleteReminderHandler(IReminderRepository repo, TimeProvider clock)
{
    public async Task CompleteAsync(Guid id, CancellationToken ct)
    {
        var reminder = await repo.FindByIdAsync(id, ct) ?? throw new NotFoundException();
        reminder.Complete(clock.GetUtcNow());
        await repo.SaveChangesAsync(ct);
    }

    public async Task ReopenAsync(Guid id, CancellationToken ct)
    {
        var reminder = await repo.FindByIdAsync(id, ct) ?? throw new NotFoundException();
        reminder.Reopen();
        await repo.SaveChangesAsync(ct);
    }
}
