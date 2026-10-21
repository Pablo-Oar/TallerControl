using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using TallerControl.Application.Common;
using TallerControl.Infrastructure.Invoicing;

namespace TallerControl.Api.IntegrationTests.Invoicing;

/// <summary>T-CERT-04 (F-06): la subida del certificado se valida contra la clave generada con el CSR.</summary>
public sealed class FiscalCertificateEncryptorTests
{
    private readonly FiscalCertificateEncryptor _encryptor = new(new EphemeralDataProtectionProvider());

    private static string SelfSignedPem(RSA key, DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        var request = new CertificateRequest("CN=20123456789", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(notBefore, notAfter);
        return cert.ExportCertificatePem();
    }

    private (byte[] Encrypted, RSA Key) NewKey()
    {
        var encrypted = _encryptor.GenerateAndEncryptPrivateKey();
        var key = RSA.Create();
        key.ImportPkcs8PrivateKey(_encryptor.DecryptPrivateKey(encrypted), out _);
        return (encrypted, key);
    }

    [Fact(DisplayName = "T-CERT-04: un certificado de la clave propia se acepta y devuelve su vencimiento real")]
    public void Certificado_propio_valido_devuelve_vencimiento()
    {
        var (encrypted, key) = NewKey();
        using var _ = key;
        var notAfter = DateTimeOffset.UtcNow.AddYears(2);

        var result = _encryptor.InspectCertificate(encrypted, SelfSignedPem(key, DateTimeOffset.UtcNow.AddDays(-1), notAfter));

        Assert.NotNull(result);
        Assert.True(Math.Abs((result!.Value - notAfter).TotalMinutes) < 5);
    }

    [Fact(DisplayName = "T-CERT-04: un certificado de otra clave se rechaza")]
    public void Certificado_de_otra_clave_se_rechaza()
    {
        var (encrypted, key) = NewKey();
        key.Dispose();
        using var other = RSA.Create(2048);

        var ex = Assert.Throws<BusinessRuleException>(() =>
            _encryptor.InspectCertificate(encrypted, SelfSignedPem(other, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1))));
        Assert.Contains("no corresponde", ex.Message);
    }

    [Fact(DisplayName = "T-CERT-04: un certificado vencido se rechaza")]
    public void Certificado_vencido_se_rechaza()
    {
        var (encrypted, key) = NewKey();
        using var _ = key;

        var ex = Assert.Throws<BusinessRuleException>(() =>
            _encryptor.InspectCertificate(encrypted, SelfSignedPem(key, DateTimeOffset.UtcNow.AddYears(-2), DateTimeOffset.UtcNow.AddDays(-1))));
        Assert.Contains("vencido", ex.Message);
    }

    [Fact(DisplayName = "T-CERT-04: un texto que no es un certificado PEM se rechaza")]
    public void Texto_invalido_se_rechaza()
    {
        var (encrypted, key) = NewKey();
        using var _ = key;

        Assert.Throws<BusinessRuleException>(() => _encryptor.InspectCertificate(encrypted, "esto no es un certificado"));
    }
}
