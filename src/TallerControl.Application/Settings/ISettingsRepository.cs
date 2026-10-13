using TallerControl.Domain.Tenancy;

namespace TallerControl.Application.Settings;

public interface ISettingsRepository
{
    /// <summary>El taller de la sesión. Nulo si no hay sesión (doc 07 §2).</summary>
    Task<Tenant?> FindTenantAsync(CancellationToken ct);

    /// <summary>La configuración del taller de la sesión (filtro global activo).</summary>
    Task<TenantSettings?> FindSettingsAsync(CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
