using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Users;

namespace TallerControl.Application.Auth;

/// <summary>
/// Operaciones de persistencia que necesitan los handlers de autenticación. La implementación
/// (<c>Infrastructure/Identity</c>) ignora los filtros globales por taller porque en el login el
/// taller aún no está resuelto.
/// </summary>
public interface IAuthRepository
{
    /// <summary>Usuario por email (normalizado a minúsculas). Nulo si no existe.</summary>
    Task<User?> FindUserByEmailAsync(string email, CancellationToken ct);

    /// <summary>Usuario por id. Nulo si no existe.</summary>
    Task<User?> FindUserByIdAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Membresía activa del usuario con su taller. Devuelve la primera encontrada (en el MVP cada
    /// usuario tiene una sola; D-15 confirma el modelo). Nulo si no hay ninguna activa.
    /// </summary>
    Task<MembershipWithTenant?> FindActiveMembershipAsync(Guid userId, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>Proyección: membresía + datos del taller necesarios para <c>/me</c>.</summary>
public sealed record MembershipWithTenant(Membership Membership, Tenant Tenant, decimal DefaultVatRate = 21m);
