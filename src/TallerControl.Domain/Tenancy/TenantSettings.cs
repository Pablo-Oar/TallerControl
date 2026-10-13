using TallerControl.Domain.Common;

namespace TallerControl.Domain.Tenancy;

/// <summary>Configuración del taller, 1:1 con <see cref="Tenant"/> (doc 04 §3.1).</summary>
public sealed class TenantSettings : ITenantOwned, IVersioned
{
    public const decimal DefaultVatRate = 21.00m;
    public const int DefaultQuoteValidityDays = 15;
    public const string DefaultPhoneCountryCode = "54";

    public static readonly IReadOnlyList<decimal> AllowedVatRates = [0m, 2.5m, 5m, 10.5m, 21m, 27m];

    private TenantSettings()
    {
    }

    public TenantSettings(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("La configuración necesita un taller.", nameof(tenantId));
        }

        TenantId = tenantId;
        DefaultVat = DefaultVatRate;
        QuoteValidityDays = DefaultQuoteValidityDays;
        PhoneCountryCode = DefaultPhoneCountryCode;
    }

    public Guid TenantId { get; private set; }

    public string? LogoFileKey { get; private set; }

    public decimal DefaultVat { get; private set; }

    public int QuoteValidityDays { get; private set; }

    public string PhoneCountryCode { get; private set; } = DefaultPhoneCountryCode;

    public bool QrPlateLookupEnabled { get; private set; }

    public uint Version { get; private set; }
}
