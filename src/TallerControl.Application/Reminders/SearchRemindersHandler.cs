using TallerControl.Application.Common;

namespace TallerControl.Application.Reminders;

public sealed class SearchRemindersHandler(IReminderRepository repo)
{
    public Task<Page<ReminderListDto>> HandleAsync(
        Guid? vehicleId, string? status, int page, int pageSize, CancellationToken ct) =>
        repo.SearchAsync(vehicleId, status is "completed" or "all" ? status : "pending", page, pageSize, ct);
}
