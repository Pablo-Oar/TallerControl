namespace TallerControl.Application.Reports;

public sealed class GetReceivablesReportHandler
{
    private readonly IReportsRepository _repository;

    public GetReceivablesReportHandler(IReportsRepository repository)
    {
        _repository = repository;
    }

    public Task<ReceivablesReportDto> HandleAsync(CancellationToken ct) =>
        _repository.GetReceivablesAsync(ct);
}
