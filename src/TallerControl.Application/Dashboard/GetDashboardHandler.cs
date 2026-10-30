namespace TallerControl.Application.Dashboard;

public sealed class GetDashboardHandler
{
    private readonly IDashboardRepository _repository;

    public GetDashboardHandler(IDashboardRepository repository)
    {
        _repository = repository;
    }

    public Task<DashboardDto> HandleAsync(DashboardScope scope, CancellationToken ct) =>
        _repository.GetSummaryAsync(scope, ct);
}
