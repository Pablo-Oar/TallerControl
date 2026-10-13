namespace TallerControl.Domain.Common;

/// <summary>
/// Entidad con borrado lógico (doc 04 §1). AppDbContext aplica el filtro global
/// <c>deleted_at IS NULL</c> y bloquea el borrado físico en SaveChanges.
/// </summary>
public interface ISoftDeletable
{
    DateTimeOffset? DeletedAt { get; }
    Guid? DeletedBy { get; }
    string? DeleteReason { get; }
}
