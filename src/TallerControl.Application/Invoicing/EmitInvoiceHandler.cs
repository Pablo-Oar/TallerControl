using TallerControl.Application.Common;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Invoicing;

namespace TallerControl.Application.Invoicing;

/// <summary>
/// Emite una factura electrónica ARCA para una OT o Venta de Mostrador (T-INV-01..03).
/// Flujo: valida fuente → determina tipo de comprobante → obtiene ticket WSAA → llama FECAESolicitar →
/// persiste Invoice con el CAE devuelto.
/// </summary>
public sealed class EmitInvoiceHandler
{
    private readonly IInvoicingRepository _repo;
    private readonly IArcaClient _arcaClient;
    private readonly IFiscalCertificateEncryptor _encryptor;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _clock;

    public EmitInvoiceHandler(
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

    public async Task<InvoiceDto> HandleAsync(
        InvoiceSourceType sourceType,
        Guid sourceId,
        EmitInvoiceRequest request,
        CancellationToken ct)
    {
        // Verificar que no exista ya una factura autorizada para esta fuente
        var existing = await _repo.FindAuthorizedInvoiceBySourceAsync(sourceType, sourceId, ct);
        if (existing != null)
            throw new ConflictException("Ya existe una factura emitida para este documento.");

        // Obtener datos del documento fuente (OT o Venta de Mostrador)
        var source = await _repo.GetSourceDataAsync(sourceType, sourceId, ct)
            ?? throw new NotFoundException();

        // Obtener punto de venta
        var pos = await _repo.FindPointOfSaleByIdAsync(request.FiscalPointOfSaleId, ct)
            ?? throw new NotFoundException();

        if (!pos.IsActive)
            throw new BusinessRuleException("El punto de venta no está activo.");

        // Obtener certificado activo del taller
        var cert = await _repo.FindActiveCertificateByTenantAsync(ct)
            ?? throw new BusinessRuleException("El taller no tiene un certificado ARCA activo.");

        // Determinar tipo de comprobante según condición IVA del cliente (doc 13 §4)
        var invoiceType = DetermineInvoiceType(source.CustomerVatCondition);

        // Construir credenciales y obtener ticket WSAA
        var privateKeyDer = _encryptor.DecryptPrivateKey(cert.PrivateKeyEncrypted);
        var credentials = new ArcaCertificateCredentials(privateKeyDer, cert.CertificatePem!, cert.Cuit, cert.IsHomologation);
        var ticket = await _arcaClient.GetTicketAsync(credentials, ct);

        // Calcular CUIT del receptor: obligatorio solo para Factura A
        var receptorCuit = invoiceType == InvoiceType.A
            ? NormalizeCuit(source.CustomerCuit)
            : null;

        var arcaRequest = new ArcaInvoiceRequest(
            TenantCuit: cert.Cuit.Replace("-", ""),
            PointOfSaleNumber: pos.Number,
            InvoiceTypeCode: MapInvoiceTypeToArcaCode(invoiceType),
            Concept: request.Concept,
            ReceptorCuit: receptorCuit,
            ReceptorVatConditionId: MapVatConditionToArcaCode(source.CustomerVatCondition),
            NetAmount: source.NetAmount,
            VatAmount: source.VatAmount,
            TotalAmount: source.TotalAmount,
            IssuedAt: _clock.GetUtcNow());

        var arcaResult = await _arcaClient.AuthorizeInvoiceAsync(ticket, arcaRequest, ct);

        var invoice = new Invoice(
            tenantId: _tenantContext.TenantId!.Value,
            fiscalPointOfSaleId: pos.Id,
            sourceType: sourceType,
            sourceId: sourceId,
            invoiceType: invoiceType,
            isCredit: false,
            originalInvoiceId: null,
            netAmount: source.NetAmount,
            vatAmount: source.VatAmount,
            totalAmount: source.TotalAmount,
            issuedAt: arcaRequest.IssuedAt,
            isHomologation: cert.IsHomologation);

        if (arcaResult.IsAuthorized)
            invoice.Authorize(arcaResult.Number, arcaResult.Cae!, arcaResult.CaeExpiresAt!.Value, arcaResult.RawResponse);
        else
            invoice.Reject(arcaResult.RejectionReason ?? "Rechazo sin detalle", arcaResult.RawResponse);

        await _repo.AddInvoiceAsync(invoice, ct);
        await _repo.SaveChangesAsync(ct);

        return await _repo.GetInvoiceDtoAsync(invoice.Id, ct)
               ?? throw new InvalidOperationException("No se pudo recuperar la factura recién emitida.");
    }

    // Códigos de comprobante WSFEv1: A=1, B=6, C=11, M=51
    internal static int MapInvoiceTypeToArcaCode(InvoiceType invoiceType) =>
        invoiceType switch
        {
            InvoiceType.A => 1,
            InvoiceType.B => 6,
            InvoiceType.C => 11,
            InvoiceType.M => 51,
            _ => throw new InvalidOperationException($"Tipo de comprobante desconocido: {invoiceType}")
        };

    internal static InvoiceType DetermineInvoiceType(VatCondition vatCondition) =>
        vatCondition switch
        {
            VatCondition.ResponsableInscripto => InvoiceType.A,
            _ => InvoiceType.B,
        };

    private static string NormalizeCuit(string? cuit)
    {
        if (string.IsNullOrWhiteSpace(cuit))
            throw new BusinessRuleException("El cliente con condición Responsable Inscripto debe tener CUIT registrado.");
        return cuit.Replace("-", "").Replace(" ", "");
    }

    // Códigos ARCA para condición IVA del receptor (tabla F.06)
    internal static int MapVatConditionToArcaCode(VatCondition vatCondition) =>
        vatCondition switch
        {
            VatCondition.ResponsableInscripto => 1,
            VatCondition.Exento => 4,
            VatCondition.ConsumidorFinal => 5,
            VatCondition.NoResponsable => 6,
            VatCondition.Monotributo => 13,
            _ => 5
        };
}
