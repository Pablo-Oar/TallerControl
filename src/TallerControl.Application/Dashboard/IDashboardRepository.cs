namespace TallerControl.Application.Dashboard;

public interface IDashboardRepository
{
    Task<DashboardDto> GetSummaryAsync(DashboardScope scope, CancellationToken ct);
}
