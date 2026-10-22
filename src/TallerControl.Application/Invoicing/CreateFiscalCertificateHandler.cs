using TallerControl.Application.Common;
using TallerControl.Domain.Invoicing;

namespace TallerControl.Application.Invoicing;

/// <summary>
/// Genera el par de claves RSA + CSR para un nuevo certificado ARCA (T-CERT-01).
/// La clave privada nunca se expone: sale cifrada a BD.
/// </summary>
public sealed class CreateFiscalCertificateHandler
{
    private readonly IInvoicingRepository _repo;
    private readonly IFiscalCertificateEncryptor _encryptor;
    private readonly ITenantContext _tenantContext;
    private readonly IActorContext _actorContext;

    public CreateFiscalCertificateHandler(
        IInvoicingRepository repo,
        IFiscalCertificateEncryptor encryptor,
        ITenantContext tenantContext,
        IActorContext actorContext)
    {
        _repo = repo;
        _encryptor = encryptor;
        _tenantContext = tenantContext;
        _actorContext = actorContext;
    }

    public async Task<FiscalCertificateDto> HandleAsync(CreateFiscalCertificateRequest request, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        var count = await _repo.CountCertificatesAsync(ct);
        if (count >= FiscalCertificate.MaxCuitsPerTenant)
            throw new BusinessRuleException($"Un taller puede tener como máximo {FiscalCertificate.MaxCuitsPerTenant} certificados ARCA.");

        if (request.IsPrimary)
        {
            var existing = await _repo.ListCertificatesAsync(ct);
            foreach (var c in existing)
                c.SetPrimary(false);
        }

        var encryptedKey = _encryptor.GenerateAndEncryptPrivateKey();
        var csrPem = _encryptor.GenerateCsr(encryptedKey, request.Cuit);

        var cert = new FiscalCertificate(tenantId, request.Cuit, request.Name, request.IsPrimary, encryptedKey, csrPem, request.Environment);
        await _repo.AddCertificateAsync(cert, ct);
        await _repo.SaveChangesAsync(ct);

        return MapToDto(cert);
    }

    internal static FiscalCertificateDto MapToDto(FiscalCertificate c) =>
        new(c.Id, c.Cuit, c.Name, c.IsPrimary, c.Status, c.Environment, c.CsrPem, c.ExpiresAt, c.IsActive, c.Version, c.CreatedAt);
}
