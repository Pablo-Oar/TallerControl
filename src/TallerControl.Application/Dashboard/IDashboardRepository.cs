namespace TallerControl.Application.Dashboard;

public interface IDashboardRepository
{
    Task<DashboardDto> GetSummaryAsync(CancellationToken ct);
}
