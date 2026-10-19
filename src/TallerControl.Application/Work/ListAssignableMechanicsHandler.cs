using TallerControl.Application.Users;
using TallerControl.Domain.Users;

namespace TallerControl.Application.Work;

/// <summary>
/// Caso de uso: mecánicos activos del taller a los que se puede asignar una orden (doc 06 /app/ordenes/:id "mecánico").
/// Lo usa quien puede escribir órdenes (dueño y administrativo), que no necesariamente gestiona usuarios.
/// </summary>
public sealed class ListAssignableMechanicsHandler
{
    private readonly IMembershipRepository _memberships;

    public ListAssignableMechanicsHandler(IMembershipRepository memberships)
    {
        _memberships = memberships;
    }

    public async Task<IReadOnlyList<AssignableMechanicDto>> HandleAsync(CancellationToken ct)
    {
        var all = await _memberships.ListAsync(ct);
        return all
            .Where(m => m.Role == TenantRole.Mechanic && m.Status == MembershipStatus.Active)
            .OrderBy(m => m.DisplayName)
            .Select(m => new AssignableMechanicDto(m.MembershipId, m.DisplayName))
            .ToList();
    }
}
