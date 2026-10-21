using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using TallerControl.Application.Common;
using TallerControl.Application.Invoicing;

namespace TallerControl.Infrastructure.Invoicing;

/// <summary>
/// Genera y protege claves privadas RSA para certificados ARCA.
/// Usa Data Protection API (ya persistida en BD desde E9) con purpose dedicado.
/// La clave privada nunca se expone en respuestas JSON ni en logs.
/// </summary>
internal sealed class FiscalCertificateEncryptor : IFiscalCertificateEncryptor
{
    private const string Purpose = "arca-private-key-v1";
    private const int RsaKeySize = 2048;

    private readonly IDataProtector _protector;

    public FiscalCertificateEncryptor(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public byte[] GenerateAndEncryptPrivateKey()
    {
        using var rsa = RSA.Create(RsaKeySize);
        var privateKeyDer = rsa.ExportPkcs8PrivateKey();
        return _protector.Protect(privateKeyDer);
    }

    public string GenerateCsr(byte[] encryptedPrivateKey, string cuit)
    {
        var privateKeyDer = DecryptPrivateKey(encryptedPrivateKey);

        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(privateKeyDer, out _);

        var subject = new X500DistinguishedName($"CN={cuit.Replace("-", "")},O=TallerControl,C=AR");
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // Exportar el CSR como PEM
        var csrDer = request.CreateSigningRequest();
        var base64 = Convert.ToBase64String(csrDer, Base64FormattingOptions.InsertLineBreaks);

        return new StringBuilder()
            .AppendLine("-----BEGIN CERTIFICATE REQUEST-----")
            .AppendLine(base64)
            .Append("-----END CERTIFICATE REQUEST-----")
            .ToString();
    }

    public byte[] DecryptPrivateKey(byte[] encryptedPrivateKey)
        => _protector.Unprotect(encryptedPrivateKey);

    public DateTimeOffset? InspectCertificate(byte[] encryptedPrivateKey, string certificatePem)
    {
        X509Certificate2 certificate;
        try
        {
            certificate = X509Certificate2.CreateFromPem(certificatePem.Trim());
        }
        catch (CryptographicException)
        {
            throw new BusinessRuleException(
                "El certificado no es válido. Pegá el texto completo, desde «-----BEGIN CERTIFICATE-----» hasta «-----END CERTIFICATE-----».");
        }

        using (certificate)
        {
            if (certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow)
                throw new BusinessRuleException("El certificado está vencido.");

            using var certKey = certificate.GetRSAPublicKey()
                ?? throw new BusinessRuleException("El certificado no tiene una clave RSA.");
            using var ownKey = RSA.Create();
            ownKey.ImportPkcs8PrivateKey(DecryptPrivateKey(encryptedPrivateKey), out _);

            if (!certKey.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(ownKey.ExportSubjectPublicKeyInfo()))
                throw new BusinessRuleException(
                    "Este certificado no corresponde al CSR generado acá. Usá el que ARCA emitió para ese CSR.");

            return new DateTimeOffset(certificate.NotAfter.ToUniversalTime(), TimeSpan.Zero);
        }
    }
}
