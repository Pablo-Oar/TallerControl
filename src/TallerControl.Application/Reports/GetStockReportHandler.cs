namespace TallerControl.Application.Reports;

public sealed class GetStockReportHandler
{
    private readonly IReportsRepository _repository;

    public GetStockReportHandler(IReportsRepository repository)
    {
        _repository = repository;
    }

    public Task<StockReportDto> HandleAsync(CancellationToken ct) =>
        _repository.GetStockAsync(ct);
}
