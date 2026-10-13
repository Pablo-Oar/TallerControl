namespace TallerControl.Domain.Common;

/// <summary>
/// Entidad con timestamps de auditoría en la propia fila (doc 04 §1).
/// AppDbContext los setea automáticamente en SaveChanges.
/// </summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; }
    Guid? CreatedBy { get; }
    DateTimeOffset? UpdatedAt { get; }
    Guid? UpdatedBy { get; }
}
