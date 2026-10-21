using TallerControl.Application.Common;

namespace TallerControl.Application.Invoicing;

public sealed class UploadFiscalCertificateHandler
{
    private readonly IInvoicingRepository _repo;
    private readonly IFiscalCertificateEncryptor _encryptor;

    public UploadFiscalCertificateHandler(IInvoicingRepository repo, IFiscalCertificateEncryptor encryptor)
    {
        _repo = repo;
        _encryptor = encryptor;
    }

    public async Task<FiscalCertificateDto> HandleAsync(Guid certificateId, UploadFiscalCertificateRequest request, CancellationToken ct)
    {
        var cert = await _repo.FindCertificateByIdAsync(certificateId, ct)
            ?? throw new NotFoundException();

        var expiresAt = _encryptor.InspectCertificate(cert.PrivateKeyEncrypted, request.CertificatePem)
            ?? request.ExpiresAt
            ?? throw new BusinessRuleException("Indicá la fecha de vencimiento del certificado.");

        cert.MarkUploaded(request.CertificatePem, expiresAt);
        await _repo.SaveChangesAsync(ct);

        return CreateFiscalCertificateHandler.MapToDto(cert);
    }
}
