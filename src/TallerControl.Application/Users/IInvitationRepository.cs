using TallerControl.Domain.Users;

namespace TallerControl.Application.Users;

public interface IInvitationRepository
{
    /// <summary>
    /// Devuelve la invitación activa (no aceptada, no vencida) por hash del token.
    /// Ignora el filtro de taller porque el token llega antes de que la sesión tenga taller.
    /// </summary>
    Task<Invitation?> FindByTokenHashAsync(byte[] tokenHash, CancellationToken ct);

    /// <summary>
    /// Devuelve la invitación pendiente más reciente para el email en el taller (del contexto). Nula si no existe.
    /// </summary>
    Task<Invitation?> FindPendingByEmailAsync(string email, CancellationToken ct);

    /// <summary>Verifica si el email ya es miembro activo del taller (del contexto).</summary>
    Task<bool> IsActiveMemberAsync(string email, CancellationToken ct);

    void Add(Invitation invitation);

    /// <summary>
    /// Agrega el usuario y la membresía en la misma unidad de trabajo que la invitación aceptada.
    /// El EF DbContext es compartido entre todos los repositorios del scope.
    /// </summary>
    void AddUser(User user);

    void AddMembership(Membership membership);

    Task SaveChangesAsync(CancellationToken ct);
}
