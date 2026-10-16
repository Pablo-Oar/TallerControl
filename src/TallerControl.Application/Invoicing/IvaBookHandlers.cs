namespace TallerControl.Application.Invoicing;

public sealed class GetIvaBookHandler
{
    private readonly IIvaBookRepository _repo;

    public GetIvaBookHandler(IIvaBookRepository repo) => _repo = repo;

    public Task<IvaBookDto> HandleAsync(int year, int month, CancellationToken ct)
    {
        ValidatePeriod(year, month);
        return _repo.GetIvaBookAsync(year, month, ct);
    }

    private static void ValidatePeriod(int year, int month)
    {
        if (year < 2000 || year > 2100)
            throw new ArgumentOutOfRangeException(nameof(year), "Año inválido.");
        if (month < 1 || month > 12)
            throw new ArgumentOutOfRangeException(nameof(month), "Mes inválido.");
    }
}

public sealed class GetCitiExportHandler
{
    private readonly IIvaBookRepository _repo;

    public GetCitiExportHandler(IIvaBookRepository repo) => _repo = repo;

    public Task<(byte[] ZipBytes, string FileName)> HandleAsync(int year, int month, CancellationToken ct)
    {
        if (year < 2000 || year > 2100)
            throw new ArgumentOutOfRangeException(nameof(year));
        if (month < 1 || month > 12)
            throw new ArgumentOutOfRangeException(nameof(month));

        return _repo.GetCitiZipAsync(year, month, ct);
    }
}
