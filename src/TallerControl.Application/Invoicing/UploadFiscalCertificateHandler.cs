using TallerControl.Application.Common;

namespace TallerControl.Application.Invoicing;

public sealed class UploadFiscalCertificateHandler
{
    private readonly IInvoicingRepository _repo;

    public UploadFiscalCertificateHandler(IInvoicingRepository repo)
        => _repo = repo;

    public async Task<FiscalCertificateDto> HandleAsync(Guid certificateId, UploadFiscalCertificateRequest request, CancellationToken ct)
    {
        var cert = await _repo.FindCertificateByIdAsync(certificateId, ct)
            ?? throw new NotFoundException();

        cert.MarkUploaded(request.CertificatePem, request.ExpiresAt);
        await _repo.SaveChangesAsync(ct);

        return CreateFiscalCertificateHandler.MapToDto(cert);
    }
}
