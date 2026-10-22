using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Common;
using TallerControl.Application.Invoicing;
using TallerControl.Domain.CounterSales;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Invoicing;
using TallerControl.Domain.Work;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Invoicing;

internal sealed class EfInvoicingRepository : IInvoicingRepository
{
    private readonly AppDbContext _db;

    public EfInvoicingRepository(AppDbContext db) => _db = db;

    // ── Certificates ───────────────────────────────────────────────────────────

    public Task<FiscalCertificate?> FindCertificateByIdAsync(Guid id, CancellationToken ct) =>
        _db.Set<FiscalCertificate>().FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<FiscalCertificate?> FindActiveCertificateByTenantAsync(CancellationToken ct) =>
        _db.Set<FiscalCertificate>()
            .Where(c => c.IsPrimary && c.Status == FiscalCertificateStatus.Active)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<FiscalCertificate>> ListCertificatesAsync(CancellationToken ct) =>
        await _db.Set<FiscalCertificate>().OrderBy(c => c.CreatedAt).ToListAsync(ct);

    public Task<int> CountCertificatesAsync(CancellationToken ct) =>
        _db.Set<FiscalCertificate>().CountAsync(ct);

    public async Task AddCertificateAsync(FiscalCertificate certificate, CancellationToken ct) =>
        await _db.Set<FiscalCertificate>().AddAsync(certificate, ct);

    // ── Points of Sale ─────────────────────────────────────────────────────────

    public Task<FiscalPointOfSale?> FindPointOfSaleByIdAsync(Guid id, CancellationToken ct) =>
        _db.Set<FiscalPointOfSale>().FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> PointOfSaleNumberExistsAsync(Guid? excludeId, int number, CancellationToken ct) =>
        _db.Set<FiscalPointOfSale>()
            .Where(p => p.Number == number && (excludeId == null || p.Id != excludeId))
            .AnyAsync(ct);

    public async Task<IReadOnlyList<FiscalPointOfSale>> ListPointsOfSaleAsync(CancellationToken ct) =>
        await _db.Set<FiscalPointOfSale>().OrderBy(p => p.Number).ToListAsync(ct);

    public async Task AddPointOfSaleAsync(FiscalPointOfSale pos, CancellationToken ct) =>
        await _db.Set<FiscalPointOfSale>().AddAsync(pos, ct);

    // ── Source documents ────────────────────────────────────────────────────────

    public async Task<InvoiceSourceData?> GetSourceDataAsync(
        InvoiceSourceType sourceType, Guid sourceId, CancellationToken ct)
    {
        if (sourceType == InvoiceSourceType.WorkOrder)
        {
            var data = await (
                from wo in _db.Set<WorkOrder>()
                join cu in _db.Set<Customer>() on wo.CustomerId equals cu.Id
                where wo.Id == sourceId
                select new InvoiceSourceData(
                    wo.CustomerId,
                    cu.Name,
                    cu.DocNumber,
                    cu.VatCondition,
                    wo.SubtotalNet,
                    wo.VatAmount,
                    wo.Total)
            ).FirstOrDefaultAsync(ct);

            return data;
        }

        if (sourceType == InvoiceSourceType.CounterSale)
        {
            var cs = await _db.Set<CounterSale>()
                .FirstOrDefaultAsync(s => s.Id == sourceId, ct);

            if (cs == null) return null;

            // Cliente opcional en mostrador
            if (cs.CustomerId == null)
            {
                return new InvoiceSourceData(
                    CustomerId: Guid.Empty,
                    CustomerName: "Consumidor Final",
                    CustomerCuit: null,
                    CustomerVatCondition: VatCondition.ConsumidorFinal,
                    NetAmount: cs.SubtotalNet,
                    VatAmount: cs.VatAmount,
                    TotalAmount: cs.Total);
            }

            var csCustomer = await _db.Set<Customer>()
                .FirstOrDefaultAsync(c => c.Id == cs.CustomerId, ct);

            if (csCustomer == null) return null;

            return new InvoiceSourceData(
                CustomerId: cs.CustomerId.Value,
                CustomerName: csCustomer.Name,
                CustomerCuit: csCustomer.DocNumber,
                CustomerVatCondition: csCustomer.VatCondition,
                NetAmount: cs.SubtotalNet,
                VatAmount: cs.VatAmount,
                TotalAmount: cs.Total);
        }

        return null;
    }

    // ── Invoices ────────────────────────────────────────────────────────────────

    public Task<Invoice?> FindInvoiceByIdAsync(Guid id, CancellationToken ct) =>
        _db.Set<Invoice>().FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<Invoice?> FindAuthorizedInvoiceBySourceAsync(
        InvoiceSourceType sourceType, Guid sourceId, CancellationToken ct) =>
        _db.Set<Invoice>()
            .Where(i => i.SourceType == sourceType && i.SourceId == sourceId
                        && i.Status == InvoiceStatus.Authorized && !i.IsCredit)
            .FirstOrDefaultAsync(ct);

    public async Task<Page<InvoiceListDto>> SearchInvoicesAsync(
        InvoiceStatus? status, InvoiceSourceType? sourceType, Guid? sourceId, int page, int pageSize, CancellationToken ct)
    {
        var query = _db.Set<Invoice>().AsQueryable();

        if (status.HasValue)
            query = query.Where(i => i.Status == status.Value);
        if (sourceType.HasValue)
            query = query.Where(i => i.SourceType == sourceType.Value);
        if (sourceId.HasValue)
            query = query.Where(i => i.SourceId == sourceId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(i => i.IssuedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InvoiceListDto(
                i.Id,
                i.Number,
                i.InvoiceType,
                i.Status,
                i.IsCredit,
                i.TotalAmount,
                i.IssuedAt,
                i.SourceType,
                i.SourceId,
                i.IsHomologation))
            .ToListAsync(ct);

        return new Page<InvoiceListDto>(items, total, page, pageSize);
    }

    public async Task<InvoiceDto?> GetInvoiceDtoAsync(Guid id, CancellationToken ct)
    {
        var i = await _db.Set<Invoice>()
            .Include(x => x.FiscalPointOfSale)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (i == null) return null;

        return new InvoiceDto(
            i.Id,
            i.FiscalPointOfSaleId,
            i.FiscalPointOfSale.Number,
            i.SourceType,
            i.SourceId,
            i.InvoiceType,
            i.Number,
            i.Cae,
            i.CaeExpiresAt,
            i.Status,
            i.IsCredit,
            i.OriginalInvoiceId,
            i.NetAmount,
            i.VatAmount,
            i.TotalAmount,
            i.IssuedAt,
            i.RejectionReason,
            i.CreatedAt,
            i.IsHomologation);
    }

    public async Task AddInvoiceAsync(Invoice invoice, CancellationToken ct) =>
        await _db.Set<Invoice>().AddAsync(invoice, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        _db.SaveChangesAsync(ct);
}
