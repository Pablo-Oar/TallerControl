using System.Security.Cryptography;
using TallerControl.Domain.Common;

namespace TallerControl.Domain.Users;

/// <summary>
/// Invitación a un taller (doc 04 §3.1, REQ-02). El token en claro se envía al email;
/// la base guarda solo el hash SHA-256. Vence a las 24 h.
/// </summary>
public sealed class Invitation : Entity, ITenantOwned, IAudited
{
    public static readonly TimeSpan Validity = TimeSpan.FromHours(24);

    private readonly List<string> _permissions = [];

    private Invitation()
    {
    }

    private Invitation(Guid tenantId, string email, TenantRole role,
        IEnumerable<string>? permissions, byte[] tokenHash, DateTimeOffset expiresAt)
    {
        TenantId = tenantId;
        Email = User.ValidateEmail(email);
        Role = role;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;

        if (permissions is not null)
        {
            _permissions.AddRange(permissions);
        }
    }

    public Guid TenantId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public TenantRole Role { get; private set; }

    public IReadOnlyList<string> Permissions => _permissions;

    [AuditIgnore]
    public byte[] TokenHash { get; private set; } = [];

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    /// <summary>
    /// Crea la invitación y devuelve el token en claro (para el email).
    /// El token no se almacena: solo su hash.
    /// </summary>
    public static (Invitation invitation, string token) Create(
        Guid tenantId, string email, TenantRole role,
        IEnumerable<string>? permissions, DateTimeOffset now)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var hash = SHA256.HashData(Convert.FromBase64String(
            token.Replace('-', '+').Replace('_', '/') +
            new string('=', (4 - token.Length % 4) % 4)));

        var invitation = new Invitation(tenantId, email, role, permissions, hash, now + Validity);
        return (invitation, token);
    }

    public static byte[] HashToken(string token) =>
        SHA256.HashData(Convert.FromBase64String(
            token.Replace('-', '+').Replace('_', '/') +
            new string('=', (4 - token.Length % 4) % 4)));

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public bool IsAccepted => AcceptedAt.HasValue;

    public void Accept(DateTimeOffset now)
    {
        if (IsAccepted)
        {
            throw new InvalidOperationException("La invitación ya fue aceptada.");
        }

        if (IsExpired(now))
        {
            throw new InvalidOperationException("La invitación venció.");
        }

        AcceptedAt = now;
    }
}
