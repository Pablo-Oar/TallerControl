using TallerControl.Application.Invoicing;

namespace TallerControl.Api.IntegrationTests.Invoicing;

/// <summary>
/// Stub de IFiscalCertificateEncryptor para tests de integración.
/// No usa Data Protection API; devuelve valores fijos para que FakeArcaClient los ignore.
/// </summary>
internal sealed class FakeFiscalCertificateEncryptor : IFiscalCertificateEncryptor
{
    // Marcador que usamos como "clave cifrada" en las factories de test.
    public static readonly byte[] FakeEncryptedKey = [1, 2, 3];
    private static readonly byte[] FakePrivateKeyDer = new byte[128]; // 128 bytes nulos

    public byte[] GenerateAndEncryptPrivateKey() => FakeEncryptedKey;

    public string GenerateCsr(byte[] encryptedPrivateKey, string cuit) =>
        $"-----BEGIN CERTIFICATE REQUEST-----\nFAKECSR_{cuit}\n-----END CERTIFICATE REQUEST-----";

    public byte[] DecryptPrivateKey(byte[] encryptedPrivateKey) => FakePrivateKeyDer;
}
