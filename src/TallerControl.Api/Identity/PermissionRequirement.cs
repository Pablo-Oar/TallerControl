using Microsoft.AspNetCore.Authorization;
using TallerControl.Application.Auth;
using TallerControl.Domain.Users;
using TallerControl.Infrastructure.Identity;

namespace TallerControl.Api.Identity;

/// <summary>
/// Política de autorización basada en permisos efectivos de la membresía (doc 07 §3).
/// Uso: <c>endpoint.RequirePermission(Permission.UsersManage)</c>
///   o  <c>endpoint.RequireAnyPermission(Permission.WorkOrdersWrite, Permission.WorkOrdersAssignSelf)</c>.
/// </summary>
public static class PermissionRequirement
{
    /// <summary>
    /// Registra todas las políticas de permisos. Llamar desde <c>AddAuthorization</c>.
    /// </summary>
    public static AuthorizationOptions AddPermissionPolicies(this AuthorizationOptions options)
    {
        foreach (var permission in Permission.ForOwner)
        {
            options.AddPolicy(PermissionPolicy(permission),
                policy => policy.RequireAuthenticatedUser()
                                .AddRequirements(new HasPermissionRequirement(permission)));
        }

        return options;
    }

    public static RouteHandlerBuilder RequirePermission(
        this RouteHandlerBuilder builder,
        string permission) =>
        builder.RequireAuthorization(PermissionPolicy(permission));

    /// <summary>
    /// Requiere que el usuario tenga AL MENOS UNO de los permisos indicados (OR).
    /// Útil cuando roles diferentes llegan al mismo endpoint con permisos distintos
    /// (ej. workorders.write para O/A y workorders.assign_self para M).
    /// </summary>
    public static RouteHandlerBuilder RequireAnyPermission(
        this RouteHandlerBuilder builder,
        params string[] permissions) =>
        builder.RequireAuthorization(policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(new HasAnyPermissionRequirement(permissions));
        });

    internal static string PermissionPolicy(string permission) => $"perm:{permission}";
}

internal sealed class HasPermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public HasPermissionRequirement(string permission)
    {
        Permission = permission;
    }
}

internal sealed class HasAnyPermissionRequirement : IAuthorizationRequirement
{
    public IReadOnlyList<string> Permissions { get; }

    public HasAnyPermissionRequirement(string[] permissions)
    {
        Permissions = permissions;
    }
}

internal sealed class HasAnyPermissionHandler : AuthorizationHandler<HasAnyPermissionRequirement>
{
    private readonly IAuthRepository _auth;

    public HasAnyPermissionHandler(IAuthRepository auth)
    {
        _auth = auth;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        HasAnyPermissionRequirement requirement)
    {
        var userIdRaw = context.User.FindFirst(SessionClaims.UserId)?.Value;
        if (!Guid.TryParse(userIdRaw, out var userId)) return;

        var membership = await _auth.FindActiveMembershipAsync(userId, CancellationToken.None);
        if (membership is null) return;

        var effectivePerms = membership.Membership.EffectivePermissions();
        if (requirement.Permissions.Any(p => effectivePerms.Contains(p)))
            context.Succeed(requirement);
    }
}

internal sealed class HasPermissionHandler : AuthorizationHandler<HasPermissionRequirement>
{
    private readonly IAuthRepository _auth;

    public HasPermissionHandler(IAuthRepository auth)
    {
        _auth = auth;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        HasPermissionRequirement requirement)
    {
        var userIdRaw = context.User.FindFirst(SessionClaims.UserId)?.Value;
        if (!Guid.TryParse(userIdRaw, out var userId))
        {
            return;
        }

        var membership = await _auth.FindActiveMembershipAsync(userId, CancellationToken.None);
        if (membership is null)
        {
            return;
        }

        if (membership.Membership.EffectivePermissions().Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}
