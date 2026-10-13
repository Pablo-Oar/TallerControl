namespace TallerControl.Application.Settings;

/// <summary>
/// Reglas del logo del taller (REQ-04 V2): PNG, JPG o WebP de hasta 5 MB.
/// La clave en R2 es determinística y la calcula el servidor a partir del taller de la sesión,
/// así que ningún endpoint acepta un <c>fileKey</c> del cliente (doc 07 §5).
/// </summary>
public static class LogoPolicy
{
    public const long MaxSizeBytes = 5 * 1024 * 1024;

    public static IReadOnlySet<string> AllowedContentTypes { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/webp",
    };

    /// <summary>Clave fija por taller: cada logo nuevo reemplaza al anterior.</summary>
    public static string FileKeyFor(Guid tenantId) => $"tenants/{tenantId}/logo";

    /// <summary>Bytes que hay que leer para reconocer cualquiera de los formatos permitidos.</summary>
    public const int MagicBytesLength = 12;

    /// <summary>
    /// Verifica la firma binaria del archivo subido. El <c>Content-Type</c> declarado no alcanza:
    /// se comprueba el contenido real (doc 07 §5).
    /// </summary>
    public static bool MatchesMagicBytes(ReadOnlySpan<byte> prefix) =>
        IsPng(prefix) || IsJpeg(prefix) || IsWebp(prefix);

    private static bool IsPng(ReadOnlySpan<byte> p) =>
        p.Length >= 8
        && p[0] == 0x89 && p[1] == 0x50 && p[2] == 0x4E && p[3] == 0x47
        && p[4] == 0x0D && p[5] == 0x0A && p[6] == 0x1A && p[7] == 0x0A;

    private static bool IsJpeg(ReadOnlySpan<byte> p) =>
        p.Length >= 3 && p[0] == 0xFF && p[1] == 0xD8 && p[2] == 0xFF;

    // RIFF....WEBP
    private static bool IsWebp(ReadOnlySpan<byte> p) =>
        p.Length >= 12
        && p[0] == 0x52 && p[1] == 0x49 && p[2] == 0x46 && p[3] == 0x46
        && p[8] == 0x57 && p[9] == 0x45 && p[10] == 0x42 && p[11] == 0x50;
}
