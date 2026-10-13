namespace TallerControl.Domain.Common;

/// <summary>
/// Entidad de sistema con <c>tenant_id</c> opcional: los eventos globales (auditoría, outbox) no
/// pertenecen a un taller. Las consultas del taller solo ven las filas del suyo.
/// </summary>
public interface IOptionalTenantOwned
{
    Guid? TenantId { get; }
}
