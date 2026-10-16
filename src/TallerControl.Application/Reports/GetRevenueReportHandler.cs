namespace TallerControl.Application.Reports;

public sealed class GetRevenueReportHandler
{
    private readonly IReportsRepository _repository;

    public GetRevenueReportHandler(IReportsRepository repository)
    {
        _repository = repository;
    }

    public Task<RevenueReportDto> HandleAsync(DateOnly from, DateOnly to, CancellationToken ct) =>
        _repository.GetRevenueAsync(from, to, ct);
}
