namespace TallerControl.Domain.Common;

/// <summary>
/// Entidad que pertenece a un taller (REQ-01). La infraestructura le aplica el filtro global
/// por <see cref="TenantId"/> y valida el taller en cada escritura.
/// </summary>
public interface ITenantOwned
{
    Guid TenantId { get; }
}
