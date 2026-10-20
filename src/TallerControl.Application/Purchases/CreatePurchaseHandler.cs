using TallerControl.Application.Common;
using TallerControl.Application.Suppliers;
using TallerControl.Application.Tenancy;
using TallerControl.Domain.Purchases;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Application.Purchases;

public sealed class CreatePurchaseHandler
{
    private readonly IPurchaseRepository _repo;
    private readonly ISupplierRepository _supplierRepo;
    private readonly ITenantContext _tenant;
    private readonly IBranchContext _branch;
    private readonly IDocumentNumberGenerator _numbers;
    private readonly TimeProvider _clock;

    public CreatePurchaseHandler(
        IPurchaseRepository repo,
        ISupplierRepository supplierRepo,
        ITenantContext tenant,
        IBranchContext branch,
        IDocumentNumberGenerator numbers,
        TimeProvider clock)
    {
        _repo = repo;
        _supplierRepo = supplierRepo;
        _tenant = tenant;
        _branch = branch;
        _numbers = numbers;
        _clock = clock;
    }

    public async Task<PurchaseDto> HandleAsync(CreatePurchaseRequest request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        var branchId = _branch.BranchId
            ?? await _repo.GetMainBranchIdAsync(tenantId, ct)
            ?? throw new InvalidOperationException("El taller no tiene una sucursal principal configurada.");

        _ = await _supplierRepo.FindByIdAsync(tenantId, request.SupplierId, ct)
            ?? throw new NotFoundException("El proveedor no existe.");

        if (request.Items.Count == 0)
            throw new BusinessRuleException("La compra debe tener al menos un ítem.");

        await using var tx = await _repo.BeginTransactionAsync(ct);

        var number = await _numbers.NextAsync(DocumentKind.Purchase, ct);

        Purchase purchase;
        try
        {
            purchase = new Purchase(
                tenantId, branchId, request.SupplierId,
                request.InvoiceDate.ToUniversalTime(), request.PointOfSale, request.InvoiceNumber,
                request.Perceptions, request.Retentions, number);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        foreach (var itemReq in request.Items)
        {
            try
            {
                var item = new PurchaseItem(
                    tenantId, purchase.Id, itemReq.CatalogItemId,
                    itemReq.Description, itemReq.Quantity, itemReq.UnitCost, itemReq.VatRate);
                purchase.AddItem(item);
            }
            catch (ArgumentException ex)
            {
                throw new BusinessRuleException(ex.Message);
            }
        }

        await _repo.AddAsync(purchase, ct);
        await _repo.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return MapToDto(purchase);
    }

    internal static PurchaseDto MapToDto(Purchase p) =>
        new(p.Id, p.BranchId, p.SupplierId, p.DocumentNumber, p.InvoiceDate,
            p.PointOfSale, p.InvoiceNumber, p.SubtotalNet, p.VatAmount,
            p.Perceptions, p.Retentions, p.Total, p.IsExcludedFromCiti, p.Status, p.AccountId,
            p.Items.Select(i => new PurchaseItemDto(i.Id, i.CatalogItemId, i.Description,
                i.Quantity, i.UnitCost, i.VatRate, i.Total)).ToList(),
            p.Version);
}
