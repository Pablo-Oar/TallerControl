namespace TallerControl.Application.Invoicing;

/// <summary>
/// Genera y protege claves privadas RSA para los certificados ARCA.
/// La clave nunca se expone fuera del servidor.
/// </summary>
public interface IFiscalCertificateEncryptor
{
    /// <summary>Genera una clave privada RSA 2048 y devuelve los bytes DER cifrados con Data Protection API.</summary>
    byte[] GenerateAndEncryptPrivateKey();

    /// <summary>Genera el CSR en formato PEM a partir de la clave privada cifrada y el CUIT del emisor.</summary>
    string GenerateCsr(byte[] encryptedPrivateKey, string cuit);

    /// <summary>Descifra la clave privada para usarla en WSAA. Solo se debe llamar dentro del proceso de firma.</summary>
    byte[] DecryptPrivateKey(byte[] encryptedPrivateKey);
}
