using TallerControl.Application.Common;
using TallerControl.Application.Users;
using TallerControl.Domain.Users;

namespace TallerControl.Application.Work;

/// <summary>Regla común: el mecánico de una orden es un mecánico activo de este taller (la FK del cuerpo no se confía).</summary>
internal static class MechanicAssignment
{
    public static async Task EnsureAssignableAsync(IMembershipRepository memberships, Guid? mechanicId, CancellationToken ct)
    {
        if (mechanicId is not { } id) return;

        var membership = await memberships.FindByIdAsync(id, ct);
        if (membership is null || membership.Role != TenantRole.Mechanic || membership.Status != MembershipStatus.Active)
            throw new BusinessRuleException("El mecánico indicado no existe o no está activo.");
    }
}
