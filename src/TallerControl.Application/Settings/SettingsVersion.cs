namespace TallerControl.Application.Settings;

/// <summary>
/// Versión combinada de <c>tenants.xmin</c> y <c>tenant_settings.xmin</c>, como
/// <c>"{tenantVersion}.{settingsVersion}"</c>: el PUT de <c>/api/v1/settings</c> escribe las dos
/// tablas, así que el <c>If-Match</c> tiene que cubrir ambas (doc 05 §2.2).
/// </summary>
public static class SettingsVersion
{
    public static string Format(uint tenantVersion, uint settingsVersion) =>
        $"{tenantVersion}.{settingsVersion}";

    public static bool TryParse(string raw, out uint tenantVersion, out uint settingsVersion)
    {
        tenantVersion = 0;
        settingsVersion = 0;

        var parts = raw.Split('.');
        return parts.Length == 2
            && uint.TryParse(parts[0], out tenantVersion)
            && uint.TryParse(parts[1], out settingsVersion);
    }
}
