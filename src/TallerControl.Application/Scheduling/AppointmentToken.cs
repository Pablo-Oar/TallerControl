using System.Security.Cryptography;
using TallerControl.Application.Common;

namespace TallerControl.Application.Scheduling;

/// <summary>Token del enlace del turno: 256 bits en base64url; en la base solo se guarda el hash.</summary>
internal static class AppointmentToken
{
    public static (string Token, byte[] Hash) New()
    {
        var raw = RandomNumberGenerator.GetBytes(32);
        return (Convert.ToBase64String(raw).TrimEnd('=').Replace('+', '-').Replace('/', '_'), SHA256.HashData(raw));
    }

    /// <summary>Hash del token recibido; un token mal formado es un 404 (igual que uno inexistente).</summary>
    public static byte[] Hash(string tokenBase64Url)
    {
        try
        {
            var padded = tokenBase64Url.Replace('-', '+').Replace('_', '/')
                         + new string('=', (4 - tokenBase64Url.Length % 4) % 4);
            return SHA256.HashData(Convert.FromBase64String(padded));
        }
        catch (FormatException)
        {
            throw new NotFoundException();
        }
    }
}
