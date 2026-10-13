using TallerControl.Domain.Common;

namespace TallerControl.Domain.Tenancy;

/// <summary>Configuración del taller, 1:1 con <see cref="Tenant"/> (doc 04 §3.1).</summary>
public sealed class TenantSettings : ITenantOwned, IVersioned, IAudited
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

    /// <summary>Actualiza IVA y vigencia de presupuesto (REQ-04). El IVA debe estar en <see cref="AllowedVatRates"/>.</summary>
    public void UpdateSettings(decimal defaultVat, int quoteValidityDays)
    {
        if (!AllowedVatRates.Contains(defaultVat))
        {
            throw new ArgumentException(
                $"Alícuota de IVA inválida: {defaultVat}. Valores permitidos: {string.Join(", ", AllowedVatRates)}.",
                nameof(defaultVat));
        }

        if (quoteValidityDays is < 1 or > 180)
        {
            throw new ArgumentException(
                "Los días de vigencia del presupuesto deben estar entre 1 y 180.",
                nameof(quoteValidityDays));
        }

        DefaultVat = defaultVat;
        QuoteValidityDays = quoteValidityDays;
    }

    /// <summary>Fija la clave del logo ya subido a R2/RustFS (doc 05 §2.2, T-SET-03).</summary>
    public void SetLogo(string fileKey)
    {
        if (string.IsNullOrWhiteSpace(fileKey))
        {
            throw new ArgumentException("La clave del logo no puede estar vacía.", nameof(fileKey));
        }

        LogoFileKey = fileKey;
    }
}
