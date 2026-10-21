using TallerControl.Application.Common;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Invoicing;

namespace TallerControl.Application.Invoicing;

/// <summary>Datos mínimos de un documento fuente (OT o Venta de Mostrador) necesarios para emitir la factura.</summary>
public sealed record InvoiceSourceData(
    Guid CustomerId,
    string CustomerName,
    string? CustomerCuit,
    VatCondition CustomerVatCondition,
    decimal NetAmount,
    decimal VatAmount,
    decimal TotalAmount);

public interface IInvoicingRepository
{
    // ── Certificates ─────────────────────────────────────────────────────────

    Task<FiscalCertificate?> FindCertificateByIdAsync(Guid id, CancellationToken ct);
    Task<FiscalCertificate?> FindActiveCertificateByTenantAsync(CancellationToken ct);
    Task<IReadOnlyList<FiscalCertificate>> ListCertificatesAsync(CancellationToken ct);
    Task<int> CountCertificatesAsync(CancellationToken ct);
    Task AddCertificateAsync(FiscalCertificate certificate, CancellationToken ct);

    // ── Points of Sale ────────────────────────────────────────────────────────

    Task<FiscalPointOfSale?> FindPointOfSaleByIdAsync(Guid id, CancellationToken ct);
    Task<bool> PointOfSaleNumberExistsAsync(Guid? excludeId, int number, CancellationToken ct);
    Task<IReadOnlyList<FiscalPointOfSale>> ListPointsOfSaleAsync(CancellationToken ct);
    Task AddPointOfSaleAsync(FiscalPointOfSale pos, CancellationToken ct);

    // ── Source documents (cross-module read, no entity tracking) ─────────────

    Task<InvoiceSourceData?> GetSourceDataAsync(InvoiceSourceType sourceType, Guid sourceId, CancellationToken ct);

    // ── Invoices ──────────────────────────────────────────────────────────────

    Task<Invoice?> FindInvoiceByIdAsync(Guid id, CancellationToken ct);
    Task<Invoice?> FindAuthorizedInvoiceBySourceAsync(InvoiceSourceType sourceType, Guid sourceId, CancellationToken ct);
    Task<Page<InvoiceListDto>> SearchInvoicesAsync(InvoiceStatus? status, InvoiceSourceType? sourceType, Guid? sourceId, int page, int pageSize, CancellationToken ct);
    Task<InvoiceDto?> GetInvoiceDtoAsync(Guid id, CancellationToken ct);
    Task AddInvoiceAsync(Invoice invoice, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
