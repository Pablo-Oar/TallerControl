using TallerControl.Application.Common;

namespace TallerControl.Application.Reminders;

public sealed class DeleteReminderHandler(IReminderRepository repo, IActorContext actor, TimeProvider clock)
{
    public async Task HandleAsync(Guid id, CancellationToken ct)
    {
        var reminder = await repo.FindByIdAsync(id, ct) ?? throw new NotFoundException();
        reminder.Delete("Eliminado por el usuario", actor.ActorId, clock.GetUtcNow());
        await repo.SaveChangesAsync(ct);
    }
}
