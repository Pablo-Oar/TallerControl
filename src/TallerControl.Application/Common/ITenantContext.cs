namespace TallerControl.Application.Common;

/// <summary>
/// Taller de la sesión actual. Es la única fuente del <c>tenant_id</c>: ningún DTO lo acepta (REQ-01).
/// <see cref="TenantId"/> es nulo cuando no hay sesión; en ese caso las consultas no devuelven filas.
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
}
