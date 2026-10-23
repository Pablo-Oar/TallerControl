using TallerControl.Application.Common;
using TallerControl.Domain.Invoicing;

namespace TallerControl.Application.Invoicing;

/// <summary>
/// Emite una nota de crédito electrónica ARCA para anular una factura existente (T-INV-05).
/// Crea un nuevo comprobante con IsCredit=true referenciando al original.
/// </summary>
public sealed class VoidInvoiceHandler
{
    private readonly IInvoicingRepository _repo;
    private readonly IArcaClient _arcaClient;
    private readonly IFiscalCertificateEncryptor _encryptor;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _clock;

    public VoidInvoiceHandler(
        IInvoicingRepository repo,
        IArcaClient arcaClient,
        IFiscalCertificateEncryptor encryptor,
        ITenantContext tenantContext,
        TimeProvider clock)
    {
        _repo = repo;
        _arcaClient = arcaClient;
        _encryptor = encryptor;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<InvoiceDto> HandleAsync(Guid originalInvoiceId, CancellationToken ct)
    {
        var original = await _repo.FindInvoiceByIdAsync(originalInvoiceId, ct)
            ?? throw new NotFoundException();

        if (original.Status != InvoiceStatus.Authorized)
            throw new BusinessRuleException("Solo se pueden anular facturas autorizadas.");

        if (original.IsCredit)
            throw new BusinessRuleException("No se puede anular una nota de crédito.");

        if (await _repo.HasAuthorizedCreditNoteAsync(original.Id, ct))
            throw new ConflictException("Esta factura ya tiene una nota de crédito.");

        var pos = await _repo.FindPointOfSaleByIdAsync(original.FiscalPointOfSaleId, ct)
            ?? throw new NotFoundException();

        var cert = await _repo.FindActiveCertificateByTenantAsync(ct)
            ?? throw new BusinessRuleException("El taller no tiene un certificado ARCA activo.");

        // Nota de crédito: mismo tipo pero código diferente (A=3, B=8, C=13)
        var creditTypeCode = MapToCreditNoteCode(original.InvoiceType);
        var privateKeyDer = _encryptor.DecryptPrivateKey(cert.PrivateKeyEncrypted);
        var credentials = new ArcaCertificateCredentials(privateKeyDer, cert.CertificatePem!, cert.Cuit, cert.IsHomologation);
        var ticket = await _arcaClient.GetTicketAsync(credentials, ct);

        var source = await _repo.GetSourceDataAsync(original.SourceType, original.SourceId, ct);

        var arcaRequest = new ArcaInvoiceRequest(
            TenantCuit: cert.Cuit.Replace("-", ""),
            PointOfSaleNumber: pos.Number,
            InvoiceTypeCode: creditTypeCode,
            Concept: 2, // Servicios
            ReceptorCuit: source?.CustomerCuit?.Replace("-", ""),
            ReceptorVatConditionId: source != null ? EmitInvoiceHandler.MapVatConditionToArcaCode(source.CustomerVatCondition) : 5,
            NetAmount: original.NetAmount,
            VatAmount: original.VatAmount,
            TotalAmount: original.TotalAmount,
            IssuedAt: _clock.GetUtcNow());

        var arcaResult = await _arcaClient.AuthorizeInvoiceAsync(ticket, arcaRequest, ct);

        var creditNote = new Invoice(
            tenantId: _tenantContext.TenantId!.Value,
            fiscalPointOfSaleId: pos.Id,
            sourceType: original.SourceType,
            sourceId: original.SourceId,
            invoiceType: original.InvoiceType,
            netAmount: original.NetAmount,
            vatAmount: original.VatAmount,
            totalAmount: original.TotalAmount,
            issuedAt: arcaRequest.IssuedAt,
            isCredit: true,
            originalInvoiceId: original.Id,
            isHomologation: cert.IsHomologation);

        if (arcaResult.IsAuthorized)
            creditNote.Authorize(arcaResult.Number, arcaResult.Cae!, arcaResult.CaeExpiresAt!.Value, arcaResult.RawResponse);
        else
            creditNote.Reject(arcaResult.RejectionReason ?? "Rechazo sin detalle", arcaResult.RawResponse);

        await _repo.AddInvoiceAsync(creditNote, ct);
        await _repo.SaveChangesAsync(ct);

        return await _repo.GetInvoiceDtoAsync(creditNote.Id, ct)
               ?? throw new InvalidOperationException("No se pudo recuperar la nota de crédito recién emitida.");
    }

    // Notas de crédito ARCA: A→3, B→8, C→13
    private static int MapToCreditNoteCode(InvoiceType invoiceType) =>
        invoiceType switch
        {
            InvoiceType.A => 3,
            InvoiceType.B => 8,
            InvoiceType.C => 13,
            InvoiceType.M => 53,
            _ => throw new InvalidOperationException($"Tipo de comprobante sin código de crédito: {invoiceType}")
        };
}
