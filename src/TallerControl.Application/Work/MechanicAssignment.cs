using TallerControl.Application.Common;
using TallerControl.Application.Users;
using TallerControl.Domain.Users;

namespace TallerControl.Application.Work;

/// <summary>Regla común: el mecánico de una orden es un mecánico activo de este taller (la FK del cuerpo no se confía).</summary>
internal static class MechanicAssignment
{
    /// <summary>Toda orden lleva un mecánico: sin él no se crea ni se edita.</summary>
    public static async Task EnsureRequiredAsync(IMembershipRepository memberships, Guid? mechanicId, CancellationToken ct)
    {
        if (mechanicId is null)
            throw new BusinessRuleException("Asigná un mecánico a la orden.");
        await EnsureAssignableAsync(memberships, mechanicId, ct);
    }

    public static async Task EnsureAssignableAsync(IMembershipRepository memberships, Guid? mechanicId, CancellationToken ct)
    {
        if (mechanicId is not { } id) return;

        var membership = await memberships.FindByIdAsync(id, ct);
        if (membership is null || membership.Role != TenantRole.Mechanic || membership.Status != MembershipStatus.Active)
            throw new BusinessRuleException("El mecánico indicado no existe o no está activo.");
    }
}
