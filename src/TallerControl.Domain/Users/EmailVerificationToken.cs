using System.Security.Cryptography;
using TallerControl.Domain.Common;

namespace TallerControl.Domain.Users;

/// <summary>
/// Token de verificación de email para el flujo de auto-registro (D-22).
/// El token en claro se envía al email; la BD guarda solo el hash SHA-256.
/// Vence en 24 h, uso único.
/// </summary>
public sealed class EmailVerificationToken : Entity, IAudited
{
    public static readonly TimeSpan Validity = TimeSpan.FromHours(24);

    private EmailVerificationToken()
    {
    }

    private EmailVerificationToken(Guid userId, byte[] tokenHash, DateTimeOffset expiresAt)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public Guid UserId { get; private set; }

    [AuditIgnore]
    public byte[] TokenHash { get; private set; } = [];

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    /// <summary>Crea el registro y devuelve el token en claro (base64url, para el enlace del email).</summary>
    public static (EmailVerificationToken token, string plainToken) Create(Guid userId, DateTimeOffset now)
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var plain = Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var hash = HashToken(plain);
        return (new EmailVerificationToken(userId, hash, now + Validity), plain);
    }

    public static byte[] HashToken(string token) =>
        SHA256.HashData(Convert.FromBase64String(
            token.Replace('-', '+').Replace('_', '/') +
            new string('=', (4 - token.Length % 4) % 4)));

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public bool IsUsed => UsedAt.HasValue;

    public void MarkUsed(DateTimeOffset now)
    {
        if (IsUsed) throw new InvalidOperationException("El token ya fue utilizado.");
        if (IsExpired(now)) throw new InvalidOperationException("El token venció.");
        UsedAt = now;
    }
}
