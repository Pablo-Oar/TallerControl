using TallerControl.Application.Common;
using TallerControl.Infrastructure.Identity;

namespace TallerControl.Api.Identity;

/// <summary>
/// Lee el <c>tenant_id</c> desde los claims de la cookie <c>tc_session</c>. Sin sesión devuelve nulo:
/// con <see cref="ITenantContext.TenantId"/> nulo las consultas del taller no devuelven filas y las
/// escrituras se rechazan (doc 07 §1, REQ-01).
/// </summary>
internal sealed class SessionTenantContext : ITenantContext, IBranchContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SessionTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? TenantId => ReadGuid(SessionClaims.TenantId);

    // Para el MVP cada sucursal activa se resolverá cuando E3 agregue selector; por ahora, nulo.
    public Guid? BranchId => null;

    private Guid? ReadGuid(string claimType)
    {
        var raw = _httpContextAccessor.HttpContext?.User.FindFirst(claimType)?.Value;
        return Guid.TryParse(raw, out var value) ? value : null;
    }
}
