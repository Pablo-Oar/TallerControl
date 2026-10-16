using TallerControl.Application.Common;

namespace TallerControl.Application.Reminders;

public sealed class GetReminderHandler(IReminderRepository repo)
{
    public async Task<ReminderDto> HandleAsync(Guid id, CancellationToken ct) =>
        await repo.GetFullAsync(id, ct) ?? throw new NotFoundException();
}
