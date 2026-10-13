namespace TallerControl.Application.Common;

/// <summary>
/// Respuesta paginada (doc 05 §1). Parámetros: <c>?page=1&amp;pageSize=25</c> (máx 100).
/// </summary>
public sealed record Page<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    int Total);
