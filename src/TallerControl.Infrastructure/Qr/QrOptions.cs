namespace TallerControl.Infrastructure.Qr;

public sealed class QrOptions
{
    public const string Section = "Qr";

    /// <summary>Base URL del portal QR. Por defecto https://tallercontrol.com.ar. Configurable vía QR:BaseUrl.</summary>
    public string BaseUrl { get; set; } = "https://tallercontrol.com.ar";

    /// <summary>Site key pública de Cloudflare Turnstile. Solo para logging; la verificación usa el SecretKey.</summary>
    public string TurnstileSiteKey { get; set; } = string.Empty;

    /// <summary>Secret key de Cloudflare Turnstile. 1x0000000000000000000000000000000AA siempre pasa en tests.</summary>
    public string TurnstileSecretKey { get; set; } = string.Empty;
}
