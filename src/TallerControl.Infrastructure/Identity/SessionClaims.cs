namespace TallerControl.Infrastructure.Identity;

/// <summary>
/// Tipos de claim usados por la cookie <c>tc_session</c>. Centralizados para que el middleware,
/// los endpoints y los <c>ITenantContext</c>/<c>IActorContext</c> compartan los nombres exactos.
/// </summary>
public static class SessionClaims
{
    public const string UserId = "tc:uid";
    public const string TenantId = "tc:tid";
    public const string Role = "tc:role";
    public const string SecurityStamp = "tc:stamp";
}
