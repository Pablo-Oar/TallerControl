namespace TallerControl.Domain.Common;

/// <summary>
/// Entidad editable con concurrencia optimista: <see cref="Version"/> se mapea a la
/// columna de sistema <c>xmin</c> de PostgreSQL.
/// </summary>
public interface IVersioned
{
    uint Version { get; }
}
