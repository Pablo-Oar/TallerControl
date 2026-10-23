using TallerControl.Application.Common;

namespace TallerControl.Application.Invoicing;

/// <summary>F-06: da de baja un certificado. Con él revocado no se puede facturar hasta cargar uno activo.</summary>
public sealed class RevokeFiscalCertificateHandler(IInvoicingRepository repo)
{
    public async Task<FiscalCertificateDto> HandleAsync(Guid certificateId, CancellationToken ct)
    {
        var cert = await repo.FindCertificateByIdAsync(certificateId, ct)
            ?? throw new NotFoundException();

        cert.Revoke();
        await repo.SaveChangesAsync(ct);
        return CreateFiscalCertificateHandler.MapToDto(cert);
    }
}

/// <summary>
/// F-06: renueva un certificado (vencido, por vencer o revocado): genera una clave y un CSR nuevos para el mismo CUIT.
/// Queda Pendiente hasta que se cargue el certificado firmado por ARCA.
/// </summary>
public sealed class RenewFiscalCertificateHandler(IInvoicingRepository repo, IFiscalCertificateEncryptor encryptor)
{
    public async Task<FiscalCertificateDto> HandleAsync(Guid certificateId, CancellationToken ct)
    {
        var cert = await repo.FindCertificateByIdAsync(certificateId, ct)
            ?? throw new NotFoundException();

        var encryptedKey = encryptor.GenerateAndEncryptPrivateKey();
        cert.Renew(encryptedKey, encryptor.GenerateCsr(encryptedKey, cert.Cuit));
        await repo.SaveChangesAsync(ct);
        return CreateFiscalCertificateHandler.MapToDto(cert);
    }
}
