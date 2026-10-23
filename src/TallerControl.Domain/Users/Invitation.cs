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
        IEnumerable<string>? permissions, byte[] tokenHash, DateTimeOffset expiresAt, string? displayName)
    {
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
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

    /// <summary>Nombre con el que se invitó: pasa a ser el de la cuenta al aceptar.</summary>
    public string? DisplayName { get; private set; }

    /// <summary>Cuando el dueño la canceló: el enlace deja de servir.</summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>
    /// Crea la invitación y devuelve el token en claro (para el email).
    /// El token no se almacena: solo su hash.
    /// </summary>
    public static (Invitation invitation, string token) Create(
        Guid tenantId, string email, TenantRole role,
        IEnumerable<string>? permissions, DateTimeOffset now, string? displayName = null)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var hash = SHA256.HashData(Convert.FromBase64String(
            token.Replace('-', '+').Replace('_', '/') +
            new string('=', (4 - token.Length % 4) % 4)));

        var invitation = new Invitation(tenantId, email, role, permissions, hash, now + Validity, displayName);
        return (invitation, token);
    }

    /// <summary>Reenvío: emite un token nuevo (el enlace anterior deja de servir) y renueva el vencimiento.</summary>
    public string Reissue(DateTimeOffset now)
    {
        if (IsAccepted)
            throw new InvalidOperationException("La invitación ya fue aceptada.");

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        TokenHash = SHA256.HashData(tokenBytes);
        ExpiresAt = now + Validity;
        CancelledAt = null;
        return token;
    }

    public void Cancel(DateTimeOffset now)
    {
        if (IsAccepted)
            throw new InvalidOperationException("La invitación ya fue aceptada: no se puede cancelar.");

        CancelledAt ??= now;
    }

    /// <summary>Pendiente = ni aceptada ni cancelada (puede estar vencida: se reenvía).</summary>
    public bool IsPending => !IsAccepted && CancelledAt is null;

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

        if (CancelledAt is not null)
        {
            throw new InvalidOperationException("La invitación fue cancelada.");
        }

        if (IsExpired(now))
        {
            throw new InvalidOperationException("La invitación venció.");
        }

        AcceptedAt = now;
    }
}
