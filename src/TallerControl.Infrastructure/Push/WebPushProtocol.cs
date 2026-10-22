using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TallerControl.Infrastructure.Push;

/// <summary>
/// Web Push sin dependencias: cifrado de contenido <c>aes128gcm</c> (RFC 8291 / RFC 8188) y autenticación VAPID
/// (RFC 8292, JWT ES256). Todas las claves y secretos viajan en base64url.
/// </summary>
internal static class WebPushProtocol
{
    private const int RecordSize = 4096;

    /// <summary>Cifra <paramref name="payload"/> para la suscripción (<paramref name="uaPublic"/> = p256dh, <paramref name="authSecret"/> = auth).</summary>
    public static byte[] Encrypt(byte[] payload, byte[] uaPublic, byte[] authSecret)
    {
        using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        return Encrypt(payload, uaPublic, authSecret, ephemeral, RandomNumberGenerator.GetBytes(16));
    }

    internal static byte[] Encrypt(
        byte[] payload, byte[] uaPublic, byte[] authSecret, ECDiffieHellman ephemeral, byte[] salt)
    {
        if (uaPublic.Length != 65 || uaPublic[0] != 0x04)
            throw new ArgumentException("La clave p256dh debe ser un punto P-256 sin comprimir (65 bytes).", nameof(uaPublic));
        if (authSecret.Length != 16)
            throw new ArgumentException("El secreto auth debe tener 16 bytes.", nameof(authSecret));
        if (payload.Length > RecordSize - 17 - 1)
            throw new ArgumentException("El aviso es demasiado largo para un solo registro.", nameof(payload));

        var asPublic = ExportPublic(ephemeral);

        using var ua = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = uaPublic[1..33], Y = uaPublic[33..65] },
        });
        var shared = ephemeral.DeriveRawSecretAgreement(ua.PublicKey);

        // IKM = HKDF(auth_secret, ecdh, "WebPush: info\0" || ua_public || as_public, 32)
        var prkKey = HKDF.Extract(HashAlgorithmName.SHA256, shared, authSecret);
        var keyInfo = Concat(Encoding.ASCII.GetBytes("WebPush: info\0"), uaPublic, asPublic);
        var ikm = HKDF.Expand(HashAlgorithmName.SHA256, prkKey, 32, keyInfo);

        var prk = HKDF.Extract(HashAlgorithmName.SHA256, ikm, salt);
        var cek = HKDF.Expand(HashAlgorithmName.SHA256, prk, 16, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
        var nonce = HKDF.Expand(HashAlgorithmName.SHA256, prk, 12, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));

        // Un solo registro: datos || 0x02 (delimitador del último registro).
        var plaintext = Concat(payload, [0x02]);
        var cipher = new byte[plaintext.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(cek, 16))
            aes.Encrypt(nonce, plaintext, cipher, tag);

        // Cabecera: salt(16) || record size (4, BE) || idlen(1) || keyid (= as_public, 65)
        var header = new byte[16 + 4 + 1 + asPublic.Length];
        salt.CopyTo(header, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(16, 4), RecordSize);
        header[20] = (byte)asPublic.Length;
        asPublic.CopyTo(header, 21);

        return Concat(header, cipher, tag);
    }

    /// <summary>Valor del encabezado <c>Authorization</c> para el servicio push (<c>vapid t=…, k=…</c>).</summary>
    public static string VapidAuthorization(
        string endpoint, string subject, string publicKey, string privateKey, DateTimeOffset now)
    {
        var uri = new Uri(endpoint);
        var audience = $"{uri.Scheme}://{uri.Authority}";

        var header = Base64Url(Encoding.UTF8.GetBytes("""{"typ":"JWT","alg":"ES256"}"""));
        var claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            aud = audience,
            exp = now.AddHours(12).ToUnixTimeSeconds(),
            sub = subject,
        }));
        var signingInput = $"{header}.{claims}";

        var pub = FromBase64Url(publicKey);
        using var ecdsa = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = FromBase64Url(privateKey),
            Q = new ECPoint { X = pub[1..33], Y = pub[33..65] },
        });
        var signature = ecdsa.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256);

        return $"vapid t={signingInput}.{Base64Url(signature)}, k={publicKey}";
    }

    /// <summary>Par de claves VAPID nuevo: (clave pública sin comprimir, clave privada), ambas en base64url.</summary>
    public static (string PublicKey, string PrivateKey) GenerateVapidKeys()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = ecdsa.ExportParameters(includePrivateParameters: true);
        var pub = Concat([0x04], p.Q.X!, p.Q.Y!);
        return (Base64Url(pub), Base64Url(p.D!));
    }

    internal static byte[] ExportPublic(ECDiffieHellman key)
    {
        var p = key.ExportParameters(includePrivateParameters: false);
        return Concat([0x04], p.Q.X!, p.Q.Y!);
    }

    public static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] FromBase64Url(string value)
    {
        var s = value.Trim().Replace('-', '+').Replace('_', '/');
        s = s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
        return Convert.FromBase64String(s);
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var result = new byte[parts.Sum(p => p.Length)];
        var offset = 0;
        foreach (var part in parts)
        {
            part.CopyTo(result, offset);
            offset += part.Length;
        }

        return result;
    }
}
