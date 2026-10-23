using TallerControl.Application.Common;
using TallerControl.Domain.Invoicing;

namespace TallerControl.Application.Invoicing;

public interface IInvoicePdfGenerator
{
    Task<byte[]> GenerateAsync(Guid invoiceId, CancellationToken ct);
}

/// <summary>PDF del comprobante autorizado (con CAE y QR fiscal). Uno pendiente o rechazado no tiene PDF.</summary>
public sealed class GetInvoicePdfHandler(IInvoicingRepository repo, IInvoicePdfGenerator pdf)
{
    public async Task<(byte[] Bytes, string FileName)> HandleAsync(Guid invoiceId, CancellationToken ct)
    {
        var invoice = await repo.FindInvoiceByIdAsync(invoiceId, ct) ?? throw new NotFoundException();
        if (invoice.Status != InvoiceStatus.Authorized)
            throw new BusinessRuleException("Solo los comprobantes autorizados por ARCA tienen PDF.");

        var kind = invoice.IsCredit ? "nota-de-credito" : "factura";
        return (await pdf.GenerateAsync(invoiceId, ct), $"{kind}-{invoice.InvoiceType.ToString().ToLowerInvariant()}-{invoice.Number:D8}.pdf");
    }
}
