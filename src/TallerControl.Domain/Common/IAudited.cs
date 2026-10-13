namespace TallerControl.Domain.Common;

/// <summary>
/// Marca las entidades cuyas altas, cambios y borrados se registran en <c>audit_events</c> (REQ-19).
/// El registro se escribe en la misma transacción del cambio.
/// </summary>
public interface IAudited
{
}
