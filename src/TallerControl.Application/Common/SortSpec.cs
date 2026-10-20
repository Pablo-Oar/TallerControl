namespace TallerControl.Application.Common;

/// <summary>
/// Orden pedido por el cliente en un listado (<c>?sort=total&amp;dir=desc</c>). La clave se valida contra una lista
/// cerrada en cada repositorio; una clave desconocida cae en el orden por defecto (nunca se arma SQL con ella).
/// </summary>
public readonly record struct SortSpec(string? Key, bool Descending)
{
    public static SortSpec Parse(string? sort, string? dir) =>
        new(string.IsNullOrWhiteSpace(sort) ? null : sort.Trim().ToLowerInvariant(),
            string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase));
}
