using TallerControl.Application.Common;

namespace TallerControl.Application.Reminders;

public sealed class SearchRemindersHandler(IReminderRepository repo)
{
    public Task<Page<ReminderListDto>> HandleAsync(
        Guid? vehicleId, int page, int pageSize, CancellationToken ct) =>
        repo.SearchAsync(vehicleId, page, pageSize, ct);
}
