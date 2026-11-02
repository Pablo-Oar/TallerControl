using TallerControl.Domain.Common;

namespace TallerControl.Domain.Tenancy;

/// <summary>Configuración del taller, 1:1 con <see cref="Tenant"/> (doc 04 §3.1).</summary>
public sealed class TenantSettings : ITenantOwned, IVersioned, IAudited
{
    public const decimal DefaultVatRate = 21.00m;
    public const int DefaultQuoteValidityDays = 15;
    public const string DefaultPhoneCountryCode = "54";

    // Umbrales del Inicio (D-42): valores por defecto y rangos permitidos.
    public const int DefaultHotQuoteHours = 48;
    public const int DefaultStuckOrderDays = 3;
    public const int DefaultAgedReceivableDays = 30;
    public const int MaxHotQuoteHours = 720;
    public const int MaxStuckOrderDays = 60;
    public const int MaxAgedReceivableDays = 365;

    // Duración con la que se reserva un turno cuando el servicio del catálogo no tiene una propia.
    public const int DefaultServiceMinutesValue = 30;
    public const int MinServiceMinutes = 5;
    public const int MaxServiceMinutes = 480;

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
        HotQuoteHours = DefaultHotQuoteHours;
        StuckOrderDays = DefaultStuckOrderDays;
        AgedReceivableDays = DefaultAgedReceivableDays;
        DefaultServiceMinutes = DefaultServiceMinutesValue;
    }

    public Guid TenantId { get; private set; }

    public string? LogoFileKey { get; private set; }

    public decimal DefaultVat { get; private set; }

    public int QuoteValidityDays { get; private set; }

    public string PhoneCountryCode { get; private set; } = DefaultPhoneCountryCode;

    public bool QrPlateLookupEnabled { get; private set; }

    /// <summary>Horas sin respuesta para que un presupuesto enviado figure como «caliente» en el Inicio (D-42).</summary>
    public int HotQuoteHours { get; private set; } = DefaultHotQuoteHours;

    /// <summary>Días en el mismo estado para que una orden figure como «trabada» en el Inicio (D-42).</summary>
    public int StuckOrderDays { get; private set; } = DefaultStuckOrderDays;

    /// <summary>Minutos que dura un servicio al reservar un turno si el catálogo no le cargó duración.</summary>
    public int DefaultServiceMinutes { get; private set; } = DefaultServiceMinutesValue;

    /// <summary>Días con saldo pendiente para marcar una orden como «saldo antiguo» en el Inicio (D-39, D-42).</summary>
    public int AgedReceivableDays { get; private set; } = DefaultAgedReceivableDays;

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

    /// <summary>Actualiza los umbrales del Inicio (D-42). Cada valor debe estar dentro de su rango.</summary>
    public void UpdateDashboardThresholds(int hotQuoteHours, int stuckOrderDays, int agedReceivableDays)
    {
        if (hotQuoteHours is < 1 or > MaxHotQuoteHours)
        {
            throw new ArgumentException(
                $"Las horas para un presupuesto caliente deben estar entre 1 y {MaxHotQuoteHours}.",
                nameof(hotQuoteHours));
        }

        if (stuckOrderDays is < 1 or > MaxStuckOrderDays)
        {
            throw new ArgumentException(
                $"Los días para una orden trabada deben estar entre 1 y {MaxStuckOrderDays}.",
                nameof(stuckOrderDays));
        }

        if (agedReceivableDays is < 1 or > MaxAgedReceivableDays)
        {
            throw new ArgumentException(
                $"Los días para un saldo antiguo deben estar entre 1 y {MaxAgedReceivableDays}.",
                nameof(agedReceivableDays));
        }

        HotQuoteHours = hotQuoteHours;
        StuckOrderDays = stuckOrderDays;
        AgedReceivableDays = agedReceivableDays;
    }

    /// <summary>Actualiza la duración por defecto de un servicio (turnos). Debe estar entre 5 y 480 minutos.</summary>
    public void UpdateDefaultServiceMinutes(int minutes)
    {
        if (minutes is < MinServiceMinutes or > MaxServiceMinutes)
        {
            throw new ArgumentException(
                $"La duración por defecto de un servicio debe estar entre {MinServiceMinutes} y {MaxServiceMinutes} minutos.",
                nameof(minutes));
        }

        DefaultServiceMinutes = minutes;
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
