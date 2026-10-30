using TallerControl.Application.Common;

namespace TallerControl.Application.Settings;

/// <summary>
/// Caso de uso: editar la configuración del taller (doc 05 §2.2, REQ-04).
/// Escribe <c>tenants</c> y <c>tenant_settings</c> en la misma transacción; el <c>If-Match</c>
/// que llega ya viene separado en los dos valores de <c>xmin</c> (ver <see cref="SettingsMapper"/>).
/// - T-SET-02: alícuota fuera de <see cref="Domain.Tenancy.TenantSettings.AllowedVatRates"/> → 422.
/// </summary>
public sealed class UpdateSettingsHandler
{
    private readonly ISettingsRepository _repo;
    private readonly IObjectStorage _storage;

    public UpdateSettingsHandler(ISettingsRepository repo, IObjectStorage storage)
    {
        _repo = repo;
        _storage = storage;
    }

    public async Task<SettingsDto> HandleAsync(
        UpdateSettingsRequest request,
        uint expectedTenantVersion,
        uint expectedSettingsVersion,
        CancellationToken ct)
    {
        var tenant = await _repo.FindTenantAsync(ct) ?? throw new NotFoundException();
        var settings = await _repo.FindSettingsAsync(ct) ?? throw new NotFoundException();

        if (tenant.Version != expectedTenantVersion || settings.Version != expectedSettingsVersion)
        {
            throw new ConcurrencyException();
        }

        try
        {
            tenant.UpdateSettings(
                request.Name,
                request.LegalName,
                request.TaxId,
                request.Address,
                request.Phone,
                request.TimeZone);

            settings.UpdateSettings(request.DefaultVatRate, request.QuoteValidityDays);

            // Los umbrales del Inicio (D-42) son opcionales en el cuerpo: sin valor se conserva el actual.
            settings.UpdateDashboardThresholds(
                request.HotQuoteHours ?? settings.HotQuoteHours,
                request.StuckOrderDays ?? settings.StuckOrderDays,
                request.AgedReceivableDays ?? settings.AgedReceivableDays);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);

        return SettingsMapper.ToDto(tenant, settings, _storage);
    }
}
