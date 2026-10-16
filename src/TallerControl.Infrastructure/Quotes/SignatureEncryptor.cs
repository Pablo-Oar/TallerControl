using System.Text;
using Microsoft.AspNetCore.DataProtection;
using TallerControl.Application.PublicLinks;

namespace TallerControl.Infrastructure.Quotes;

/// <summary>
/// Cifra el DNI del firmante con Data Protection API (RF-R4, F-16).
/// Nunca expone el texto plano; el cifrado es simétrico y dependiente de las claves del tenant.
/// </summary>
internal sealed class SignatureEncryptor : ISignatureEncryptor
{
    private const string Purpose = "signature-doc-number-v1";

    private readonly IDataProtector _protector;

    public SignatureEncryptor(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public byte[] Encrypt(string plainText)
    {
        var bytes = Encoding.UTF8.GetBytes(plainText);
        return _protector.Protect(bytes);
    }

    public string Decrypt(byte[] cipherText)
    {
        var bytes = _protector.Unprotect(cipherText);
        return Encoding.UTF8.GetString(bytes);
    }
}
