namespace TallerControl.Application.PublicLinks;

/// <summary>
/// Reglas de negocio para las firmas de presupuestos (RF-R4, F-16).
/// </summary>
public static class SignaturePolicy
{
    public static readonly IReadOnlySet<string> AllowedContentTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/png",
            "image/jpeg",
        };

    /// <summary>Tamaño máximo de la imagen de firma (500 KB).</summary>
    public const long MaxSizeBytes = 500 * 1024;

    /// <summary>Prefijo de las claves de objeto para firmas.</summary>
    public const string KeyPrefix = "signatures";

    public static string NewFileKey() => $"{KeyPrefix}/{Guid.CreateVersion7()}";

    public static bool IsValidFileKey(string key) =>
        key.StartsWith(KeyPrefix + "/", StringComparison.OrdinalIgnoreCase);
}
