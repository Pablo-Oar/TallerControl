namespace TallerControl.Application.Work;

/// <summary>
/// Validación de magic bytes para los tipos de adjunto permitidos: JPEG, PNG, WebP y PDF.
/// Sigue el patrón de <c>LogoPolicy.MatchesMagicBytes</c> (doc 07 §5).
/// </summary>
public static class AttachmentMagicBytes
{
    public const int Length = 12;

    public static bool IsValid(string contentType, ReadOnlySpan<byte> prefix) =>
        contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => IsJpeg(prefix),
            "image/png" => IsPng(prefix),
            "image/webp" => IsWebp(prefix),
            "application/pdf" => IsPdf(prefix),
            _ => false,
        };

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

    // %PDF
    private static bool IsPdf(ReadOnlySpan<byte> p) =>
        p.Length >= 4 && p[0] == 0x25 && p[1] == 0x50 && p[2] == 0x44 && p[3] == 0x46;
}
