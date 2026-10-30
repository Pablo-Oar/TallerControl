using TallerControl.Application.Common;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Application.Settings;

/// <summary>Combina <see cref="Tenant"/> y <see cref="TenantSettings"/> en el DTO público (REQ-04).</summary>
internal static class SettingsMapper
{
    public static SettingsDto ToDto(Tenant tenant, TenantSettings settings, IObjectStorage storage)
    {
        var logoUrl = settings.LogoFileKey is null
            ? null
            : storage.CreatePresignedGetUrl(settings.LogoFileKey, TimeSpan.FromMinutes(10));

        return new SettingsDto(
            tenant.Name,
            tenant.LegalName,
            tenant.TaxId,
            tenant.Address,
            tenant.Phone,
            tenant.TimeZone,
            settings.DefaultVat,
            settings.QuoteValidityDays,
            logoUrl,
            SettingsVersion.Format(tenant.Version, settings.Version),
            settings.HotQuoteHours,
            settings.StuckOrderDays,
            settings.AgedReceivableDays);
    }
}
