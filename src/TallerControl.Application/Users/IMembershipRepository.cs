using TallerControl.Domain.Users;

namespace TallerControl.Application.Users;

public interface IMembershipRepository
{
    /// <summary>Lista todas las membresías del taller con los datos del usuario (doc 05 §2).</summary>
    Task<IReadOnlyList<MemberDto>> ListAsync(CancellationToken ct);

    /// <summary>Membresía por id dentro del taller (filtro global activo). Nula si no pertenece.</summary>
    Task<Membership?> FindByIdAsync(Guid membershipId, CancellationToken ct);

    /// <summary>Cuenta cuántos dueños activos tiene el taller.</summary>
    Task<int> CountActiveOwnersAsync(CancellationToken ct);

    /// <summary>Usuario por id (sin filtro de taller).</summary>
    Task<User?> FindUserByIdAsync(Guid userId, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
