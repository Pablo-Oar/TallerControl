namespace TallerControl.Application.Common;

/// <summary>
/// Sucursal activa de la sesión (D-15). En el MVP es siempre la principal del taller.
/// </summary>
public interface IBranchContext
{
    Guid? BranchId { get; }
}
