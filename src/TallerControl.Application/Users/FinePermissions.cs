using TallerControl.Application.Common;
using TallerControl.Domain.Users;

namespace TallerControl.Application.Users;

/// <summary>Los permisos extra que el dueño puede darle a un mecánico (ver precios, ver datos de contacto). Nada más.</summary>
internal static class FinePermissions
{
    public static void Validate(TenantRole role, IReadOnlyList<string>? permissions)
    {
        if (permissions is null || permissions.Count == 0) return;

        if (role != TenantRole.Mechanic)
            throw new BusinessRuleException("Los permisos adicionales solo se asignan a mecánicos.");

        var invalid = permissions.FirstOrDefault(p => !Permission.MechanicFineGrained.Contains(p));
        if (invalid is not null)
            throw new BusinessRuleException($"El permiso «{invalid}» no se puede otorgar a un mecánico.");
    }
}
