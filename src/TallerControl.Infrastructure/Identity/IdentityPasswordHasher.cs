using Microsoft.AspNetCore.Identity;
using TallerControl.Application.Auth;
using TallerControl.Domain.Users;
using MsResult = Microsoft.AspNetCore.Identity.PasswordVerificationResult;
using OurResult = TallerControl.Application.Auth.PasswordVerificationResult;

namespace TallerControl.Infrastructure.Identity;

/// <summary>
/// Implementa <see cref="IPasswordHasher"/> con el <see cref="PasswordHasher{TUser}"/> de ASP.NET
/// Core Identity: PBKDF2 con HMAC-SHA512, 100.000 iteraciones y sal de 128 bits (doc 07 §4).
/// </summary>
internal sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    // El PasswordHasher&lt;TUser&gt; usa User solo como clave de tipo; nunca lee sus propiedades.
    private static readonly User HasherKey = new("placeholder@tallercontrol.local", "placeholder");

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        return _hasher.HashPassword(HasherKey, password);
    }

    public OurResult Verify(string hash, string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(hash);
        var result = _hasher.VerifyHashedPassword(HasherKey, hash, password ?? string.Empty);
        return result switch
        {
            MsResult.Success => OurResult.Success,
            MsResult.SuccessRehashNeeded => OurResult.SuccessRehashNeeded,
            _ => OurResult.Failed,
        };
    }
}
