using System.Security.Cryptography;
using TallerControl.Domain.Common;

namespace TallerControl.Domain.Users;

/// <summary>
/// Token de restablecimiento de contraseña (doc 04 §3.1, T-AUTH-07/08).
/// El token en claro se envía al email; la base guarda solo el hash SHA-256.
/// Vence en 1 h, uso único.
/// </summary>
public sealed class PasswordReset : Entity, IAudited
{
    public static readonly TimeSpan Validity = TimeSpan.FromHours(1);

    private PasswordReset()
    {
    }

    private PasswordReset(string email, byte[] tokenHash, DateTimeOffset expiresAt)
    {
        Email = User.ValidateEmail(email);
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public string Email { get; private set; } = string.Empty;

    [AuditIgnore]
    public byte[] TokenHash { get; private set; } = [];

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    /// <summary>
    /// Crea el registro y devuelve el token en claro (para el email).
    /// </summary>
    public static (PasswordReset reset, string token) Create(string email, DateTimeOffset now)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var hash = SHA256.HashData(Convert.FromBase64String(
            token.Replace('-', '+').Replace('_', '/') +
            new string('=', (4 - token.Length % 4) % 4)));

        var reset = new PasswordReset(email, hash, now + Validity);
        return (reset, token);
    }

    public static byte[] HashToken(string token) =>
        SHA256.HashData(Convert.FromBase64String(
            token.Replace('-', '+').Replace('_', '/') +
            new string('=', (4 - token.Length % 4) % 4)));

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public bool IsUsed => UsedAt.HasValue;

    public void MarkUsed(DateTimeOffset now)
    {
        if (IsUsed)
        {
            throw new InvalidOperationException("El token ya fue utilizado.");
        }

        if (IsExpired(now))
        {
            throw new InvalidOperationException("El token venció.");
        }

        UsedAt = now;
    }
}
