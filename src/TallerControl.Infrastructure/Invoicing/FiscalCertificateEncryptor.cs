using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
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
}
