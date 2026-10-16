namespace TallerControl.Application.PublicLinks;

/// <summary>
/// Cifra y descifra datos sensibles del firmante (DNI) usando Data Protection API (RF-R4, F-16).
/// La implementación nunca expone el texto plano en logs ni en respuestas.
/// </summary>
public interface ISignatureEncryptor
{
    byte[] Encrypt(string plainText);
    string Decrypt(byte[] cipherText);
}
