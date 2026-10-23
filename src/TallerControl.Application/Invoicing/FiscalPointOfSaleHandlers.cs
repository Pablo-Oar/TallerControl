using TallerControl.Application.Common;
using TallerControl.Domain.Invoicing;

namespace TallerControl.Application.Invoicing;

public sealed class CreateFiscalPointOfSaleHandler
{
    private readonly IInvoicingRepository _repo;
    private readonly ITenantContext _tenantContext;

    public CreateFiscalPointOfSaleHandler(IInvoicingRepository repo, ITenantContext tenantContext)
    {
        _repo = repo;
        _tenantContext = tenantContext;
    }

    public async Task<FiscalPointOfSaleDto> HandleAsync(CreateFiscalPointOfSaleRequest request, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        var cert = await _repo.FindCertificateByIdAsync(request.FiscalCertificateId, ct)
            ?? throw new NotFoundException();

        if (await _repo.PointOfSaleNumberExistsAsync(null, request.Number, ct))
            throw new ConflictException("Ya existe un punto de venta con ese número.");

        var pos = new FiscalPointOfSale(tenantId, request.FiscalCertificateId, request.Number, request.Name);
        await _repo.AddPointOfSaleAsync(pos, ct);
        await _repo.SaveChangesAsync(ct);

        return MapToDto(pos, cert.Cuit);
    }

    internal static FiscalPointOfSaleDto MapToDto(FiscalPointOfSale pos, string? cuit = null) =>
        new(pos.Id, pos.FiscalCertificateId, pos.Number, pos.Name, pos.IsActive, pos.Version);
}

public sealed class UpdateFiscalPointOfSaleHandler
{
    private readonly IInvoicingRepository _repo;

    public UpdateFiscalPointOfSaleHandler(IInvoicingRepository repo) => _repo = repo;

    public async Task<FiscalPointOfSaleDto> HandleAsync(Guid id, UpdateFiscalPointOfSaleRequest request, uint version, CancellationToken ct)
    {
        var pos = await _repo.FindPointOfSaleByIdAsync(id, ct)
            ?? throw new NotFoundException();

        if (pos.Version != version)
            throw new ConcurrencyException();

        pos.Update(request.Name);
        if (request.IsActive is true) pos.Activate();
        else if (request.IsActive is false) pos.Deactivate();
        await _repo.SaveChangesAsync(ct);

        return CreateFiscalPointOfSaleHandler.MapToDto(pos);
    }
}

public sealed class ListFiscalPointsOfSaleHandler
{
    private readonly IInvoicingRepository _repo;

    public ListFiscalPointsOfSaleHandler(IInvoicingRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<FiscalPointOfSaleDto>> HandleAsync(CancellationToken ct)
    {
        var list = await _repo.ListPointsOfSaleAsync(ct);
        return list.Select(p => CreateFiscalPointOfSaleHandler.MapToDto(p)).ToList();
    }
}
