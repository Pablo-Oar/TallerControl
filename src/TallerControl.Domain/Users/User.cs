using System.Security.Cryptography;
using TallerControl.Domain.Common;

namespace TallerControl.Domain.Users;

/// <summary>
/// Identidad global (doc 04 §3.1). Un usuario puede pertenecer a varios talleres mediante
/// <see cref="Membership"/>. No pertenece a un taller: es global.
/// </summary>
public sealed class User : Entity, IVersioned, IAudited
{
    public const int DisplayNameMaxLength = 150;
    public const int MinPasswordLength = 10;
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private User()
    {
    }

    public User(string email, string displayName)
    {
        Email = ValidateEmail(email);
        DisplayName = ValidateDisplayName(displayName);
        Status = UserStatus.Pending;
        SecurityStamp = GenerateSecurityStamp();
    }

    public string Email { get; private set; } = string.Empty;

    [AuditIgnore]
    public string? PasswordHash { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public UserStatus Status { get; private set; }

    public DateTimeOffset? LockoutEnd { get; private set; }

    public int AccessFailedCount { get; private set; }

    [AuditIgnore]
    public string SecurityStamp { get; private set; } = string.Empty;

    public uint Version { get; private set; }

    public void Activate(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
        Status = UserStatus.Active;
        AccessFailedCount = 0;
        LockoutEnd = null;
    }

    public void Disable() => Status = UserStatus.Disabled;

    public bool IsLockedOut(DateTimeOffset now) =>
        LockoutEnd.HasValue && LockoutEnd.Value > now;

    public void RecordFailedLogin(DateTimeOffset now)
    {
        AccessFailedCount++;
        if (AccessFailedCount >= MaxFailedAttempts)
        {
            LockoutEnd = now + LockoutDuration;
        }
    }

    public void ResetAccessFailed()
    {
        AccessFailedCount = 0;
        LockoutEnd = null;
    }

    public void RotateSecurityStamp() => SecurityStamp = GenerateSecurityStamp();

    public void SetPasswordHash(string hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        PasswordHash = hash;
        RotateSecurityStamp();
    }

    private static string GenerateSecurityStamp() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    internal static string ValidateEmail(string email)
    {
        var trimmed = email?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed) || !trimmed.Contains('@'))
        {
            throw new ArgumentException("El email no es válido.", nameof(email));
        }

        return trimmed.ToLowerInvariant();
    }

    internal static string ValidateDisplayName(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is < 1 or > DisplayNameMaxLength)
        {
            throw new ArgumentException(
                $"El nombre debe tener entre 1 y {DisplayNameMaxLength} caracteres.", nameof(name));
        }

        return trimmed;
    }
}
