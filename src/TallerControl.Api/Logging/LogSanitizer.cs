namespace TallerControl.Api.Logging;

/// <summary>
/// Quita de las rutas lo que no debe quedar en un log (doc 07 §8): los tokens de los links públicos
/// y del QR viajan en la URL y equivalen a una credencial.
/// </summary>
public static class LogSanitizer
{
    public const string Redacted = "{token}";

    private const int TokenMinLength = 20;

    // Áreas públicas cuyo segmento siguiente es siempre un token o código: /q, /p, /o, /c.
    private static readonly HashSet<string> PublicPrefixes =
        new(StringComparer.OrdinalIgnoreCase) { "q", "p", "o", "c" };

    public static string SanitizePath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "/";
        }

        var segments = path.Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            var previousIsPublicPrefix = i == 2 && PublicPrefixes.Contains(segments[1]);
            if (segments[i].Length > 0 && (previousIsPublicPrefix || LooksLikeToken(segments[i])))
            {
                segments[i] = Redacted;
            }
        }

        return string.Join('/', segments);
    }

    /// <summary>Cadena larga de base64url que no es un UUID (los ids en la ruta sí se registran).</summary>
    private static bool LooksLikeToken(string segment)
    {
        if (segment.Length < TokenMinLength || Guid.TryParse(segment, out _))
        {
            return false;
        }

        foreach (var c in segment)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_')
            {
                return false;
            }
        }

        return true;
    }
}
