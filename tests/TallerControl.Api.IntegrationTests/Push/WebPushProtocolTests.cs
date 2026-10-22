using System.Security.Cryptography;
using System.Text;
using TallerControl.Infrastructure.Push;

namespace TallerControl.Api.IntegrationTests.Push;

/// <summary>N-04: el cifrado Web Push (RFC 8291) y la firma VAPID (RFC 8292) son los que el navegador espera.</summary>
public sealed class WebPushProtocolTests
{
    [Fact(DisplayName = "T-PUSH-01: El aviso cifrado lo puede descifrar el dispositivo suscripto")]
    public void El_dispositivo_descifra_el_aviso()
    {
        // El «navegador»: genera su par de claves y su secreto de autenticación.
        using var ua = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var uaPublic = WebPushProtocol.ExportPublic(ua);
        var authSecret = RandomNumberGenerator.GetBytes(16);

        var plain = Encoding.UTF8.GetBytes("""{"title":"Nueva orden asignada","body":"Te asignaron la orden N° 7."}""");
        var body = WebPushProtocol.Encrypt(plain, uaPublic, authSecret);

        // Descifrado del lado del dispositivo (RFC 8291 §3.4).
        var salt = body[..16];
        var idLength = body[20];
        var asPublic = body[21..(21 + idLength)];
        var cipherAndTag = body[(21 + idLength)..];

        using var asKey = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = asPublic[1..33], Y = asPublic[33..65] },
        });
        var shared = ua.DeriveRawSecretAgreement(asKey.PublicKey);
        var prkKey = HKDF.Extract(HashAlgorithmName.SHA256, shared, authSecret);
        var keyInfo = Encoding.ASCII.GetBytes("WebPush: info\0").Concat(uaPublic).Concat(asPublic).ToArray();
        var ikm = HKDF.Expand(HashAlgorithmName.SHA256, prkKey, 32, keyInfo);
        var prk = HKDF.Extract(HashAlgorithmName.SHA256, ikm, salt);
        var cek = HKDF.Expand(HashAlgorithmName.SHA256, prk, 16, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
        var nonce = HKDF.Expand(HashAlgorithmName.SHA256, prk, 12, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));

        var cipher = cipherAndTag[..^16];
        var tag = cipherAndTag[^16..];
        var decrypted = new byte[cipher.Length];
        using (var aes = new AesGcm(cek, 16))
            aes.Decrypt(nonce, cipher, tag, decrypted);

        Assert.Equal(0x02, decrypted[^1]);
        Assert.Equal(plain, decrypted[..^1]);
        Assert.Equal(4096, (body[16] << 24) | (body[17] << 16) | (body[18] << 8) | body[19]);
    }

    [Fact(DisplayName = "T-PUSH-02: La firma VAPID es un JWT ES256 que se verifica con la clave pública")]
    public void La_firma_vapid_se_verifica()
    {
        var (publicKey, privateKey) = WebPushProtocol.GenerateVapidKeys();
        var now = DateTimeOffset.UtcNow;

        var header = WebPushProtocol.VapidAuthorization(
            "https://fcm.googleapis.com/fcm/send/abc", "mailto:soporte@tallercontrol.com.ar", publicKey, privateKey, now);

        Assert.StartsWith("vapid t=", header);
        Assert.EndsWith($", k={publicKey}", header);

        var jwt = header["vapid t=".Length..header.IndexOf(", k=", StringComparison.Ordinal)].Split('.');
        Assert.Equal(3, jwt.Length);

        var claims = Encoding.UTF8.GetString(WebPushProtocol.FromBase64Url(jwt[1]));
        Assert.Contains("\"aud\":\"https://fcm.googleapis.com\"", claims);
        Assert.Contains("\"sub\":\"mailto:soporte@tallercontrol.com.ar\"", claims);

        var pub = WebPushProtocol.FromBase64Url(publicKey);
        using var verifier = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = pub[1..33], Y = pub[33..65] },
        });
        Assert.True(verifier.VerifyData(
            Encoding.ASCII.GetBytes($"{jwt[0]}.{jwt[1]}"), WebPushProtocol.FromBase64Url(jwt[2]), HashAlgorithmName.SHA256));
    }

    [Fact(DisplayName = "T-PUSH-03: Las claves VAPID generadas tienen el tamaño que exige el estándar")]
    public void Las_claves_vapid_tienen_el_tamano_correcto()
    {
        var (publicKey, privateKey) = WebPushProtocol.GenerateVapidKeys();
        Assert.Equal(65, WebPushProtocol.FromBase64Url(publicKey).Length);
        Assert.Equal(32, WebPushProtocol.FromBase64Url(privateKey).Length);
    }
}
