namespace TallerControl.Application.Settings;

/// <summary>
/// Respuesta de <c>GET</c> y <c>PUT /api/v1/settings</c> (doc 05 §2.2, REQ-04).
/// <see cref="Version"/> combina el <c>xmin</c> de <c>tenants</c> y de <c>tenant_settings</c>
/// (<c>"{tenant}.{settings}"</c>): el PUT escribe en las dos tablas, así que el <c>If-Match</c>
/// tiene que cubrir ambas.
/// </summary>
public sealed record SettingsDto(
    string Name,
    string? LegalName,
    string? TaxId,
    string? Address,
    string? Phone,
    string TimeZone,
    decimal DefaultVatRate,
    int QuoteValidityDays,
    string? LogoUrl,
    string Version);

/// <summary>Cuerpo de <c>PUT /api/v1/settings</c> (doc 05 §2.2). El logo va por su propio flujo.</summary>
public sealed record UpdateSettingsRequest(
    string Name,
    string? LegalName,
    string? TaxId,
    string? Address,
    string? Phone,
    string TimeZone,
    decimal DefaultVatRate,
    int QuoteValidityDays);

/// <summary>Cuerpo de <c>POST /api/v1/settings/logo/upload-url</c> (doc 05 §2.2, T-SET-03).</summary>
public sealed record LogoUploadUrlRequest(string ContentType, long Size);

/// <summary>
/// Respuesta de <c>POST /api/v1/settings/logo/upload-url</c>. <c>FileKey</c> es informativo:
/// el servidor lo recalcula y nunca lo acepta de vuelta del cliente (doc 07 §5).
/// </summary>
public sealed record LogoUploadUrlResponse(string UploadUrl, string FileKey);
