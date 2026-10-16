namespace TallerControl.Application.Reports;

public sealed class GetPurchasesReportHandler
{
    private readonly IReportsRepository _repository;

    public GetPurchasesReportHandler(IReportsRepository repository)
    {
        _repository = repository;
    }

    public Task<PurchasesReportDto> HandleAsync(DateOnly from, DateOnly to, CancellationToken ct) =>
        _repository.GetPurchasesAsync(from, to, ct);
}
