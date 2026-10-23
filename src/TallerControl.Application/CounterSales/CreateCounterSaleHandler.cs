using TallerControl.Application.Common;
using TallerControl.Application.Tenancy;
using TallerControl.Domain.CounterSales;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Application.CounterSales;

public sealed class CreateCounterSaleHandler
{
    private readonly ICounterSaleRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly IBranchContext _branch;
    private readonly IDocumentNumberGenerator _numbers;
    private readonly TimeProvider _clock;

    public CreateCounterSaleHandler(
        ICounterSaleRepository repo,
        ITenantContext tenant,
        IBranchContext branch,
        IDocumentNumberGenerator numbers,
        TimeProvider clock)
    {
        _repo = repo;
        _tenant = tenant;
        _branch = branch;
        _numbers = numbers;
        _clock = clock;
    }

    public async Task<CounterSaleDto> HandleAsync(CreateCounterSaleRequest request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        var branchId = _branch.BranchId
            ?? await _repo.GetMainBranchIdAsync(tenantId, ct)
            ?? throw new InvalidOperationException("El taller no tiene una sucursal principal configurada.");

        if (request.Items.Count == 0)
            throw new BusinessRuleException("La venta debe tener al menos un ítem.");

        await using var tx = await _repo.BeginTransactionAsync(ct);

        var number = await _numbers.NextAsync(DocumentKind.CounterSale, ct);
        var now = _clock.GetUtcNow();

        CounterSale sale;
        try
        {
            sale = new CounterSale(tenantId, branchId, request.CustomerId, number, now);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        foreach (var itemReq in request.Items)
        {
            try
            {
                var item = new CounterSaleItem(
                    tenantId, sale.Id, itemReq.CatalogItemId,
                    itemReq.Description, itemReq.Quantity, itemReq.UnitPrice, itemReq.VatRate);
                sale.AddItem(item);
            }
            catch (ArgumentException ex)
            {
                throw new BusinessRuleException(ex.Message);
            }
        }

        await _repo.AddAsync(sale, ct);
        await _repo.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return MapToDto(sale);
    }

    internal static CounterSaleDto MapToDto(CounterSale s) =>
        new(s.Id, s.BranchId, s.CustomerId, s.DocumentNumber, s.Date,
            s.SubtotalNet, s.VatAmount, s.Total, s.Status, s.AccountId,
            s.Items.Select(i => new CounterSaleItemDto(
                i.Id, i.CatalogItemId, i.Description, i.Quantity, i.UnitPrice,
                i.VatRate, i.NetAmount, i.VatAmount, i.Total)).ToList(),
            s.Version,
            s.CancelReason);
}
