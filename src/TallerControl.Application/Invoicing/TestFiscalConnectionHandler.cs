using TallerControl.Application.Common;

namespace TallerControl.Application.Invoicing;

/// <summary>
/// Prueba la conexión ARCA con el certificado activo del taller (T-CERT-03).
/// Llama a FECompUltimoAutorizado para verificar que el ticket WSAA funciona.
/// </summary>
public sealed class TestFiscalConnectionHandler
{
    private readonly IInvoicingRepository _repo;
    private readonly IArcaClient _arcaClient;
    private readonly IFiscalCertificateEncryptor _encryptor;
    private readonly TimeProvider _clock;

    public TestFiscalConnectionHandler(
        IInvoicingRepository repo,
        IArcaClient arcaClient,
        IFiscalCertificateEncryptor encryptor,
        TimeProvider clock)
    {
        _repo = repo;
        _arcaClient = arcaClient;
        _encryptor = encryptor;
        _clock = clock;
    }

    public async Task<TestFiscalConnectionResponse> HandleAsync(Guid pointOfSaleId, CancellationToken ct)
    {
        var pos = await _repo.FindPointOfSaleByIdAsync(pointOfSaleId, ct)
            ?? throw new NotFoundException();

        var cert = await _repo.FindCertificateByIdAsync(pos.FiscalCertificateId, ct)
            ?? throw new NotFoundException();

        if (!cert.IsActive)
            return new TestFiscalConnectionResponse(
                Success: false,
                LastInvoiceNumber: null,
                ErrorMessage: "El certificado no está activo.",
                TestedAt: _clock.GetUtcNow());

        try
        {
            var privateKeyDer = _encryptor.DecryptPrivateKey(cert.PrivateKeyEncrypted);
            var credentials = new ArcaCertificateCredentials(privateKeyDer, cert.CertificatePem!, cert.Cuit, cert.IsHomologation);
            var ticket = await _arcaClient.GetTicketAsync(credentials, ct);

            // Tipo de comprobante 11 (Factura C) como prueba genérica
            var lastNumber = await _arcaClient.GetLastAuthorizedNumberAsync(ticket, pos.Number, 11, ct);

            return new TestFiscalConnectionResponse(
                Success: true,
                LastInvoiceNumber: lastNumber,
                ErrorMessage: null,
                TestedAt: _clock.GetUtcNow());
        }
        catch (Exception ex)
        {
            return new TestFiscalConnectionResponse(
                Success: false,
                LastInvoiceNumber: null,
                ErrorMessage: ex.Message,
                TestedAt: _clock.GetUtcNow());
        }
    }
}
