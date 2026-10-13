namespace TallerControl.Application.Auth;

/// <summary>
/// Hash y verificación de contraseñas. La implementación en Infrastructure usa
/// PBKDF2 vía <c>Microsoft.AspNetCore.Identity.PasswordHasher&lt;T&gt;</c> (doc 07 §4).
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    PasswordVerificationResult Verify(string hash, string password);
}

public enum PasswordVerificationResult
{
    Failed = 0,
    Success = 1,

    /// <summary>Hash válido pero generado con parámetros desactualizados: hay que rehashear al vuelo.</summary>
    SuccessRehashNeeded = 2,
}
