using TallerControl.Application.Common;

namespace TallerControl.Infrastructure.Tenancy;

/// <summary>
/// Contexto por defecto: sin sesión. Con él las consultas no devuelven filas y las escrituras
/// de entidades del taller se rechazan. La implementación basada en la sesión llega en E2.
/// </summary>
internal sealed class NoTenantContext : ITenantContext, IBranchContext
{
    public Guid? TenantId => null;

    public Guid? BranchId => null;
}
