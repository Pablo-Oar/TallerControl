using Microsoft.AspNetCore.Authorization;
using TallerControl.Api.Identity;
using TallerControl.Application.Dashboard;
using TallerControl.Domain.Users;

namespace TallerControl.Api.Endpoints.Dashboard;

/// <summary>
/// Endpoint de resumen operativo (doc 05 §2, REQ-01, T-DASH-01, T-DASH-02).
/// GET /api/v1/dashboard → workorders.read AND payments.read (Owner y AdminStaff)
/// </summary>
internal static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        // Requiere workorders.read AND payments.read (solo Owner y AdminStaff los tienen ambos).
        // Inline policy para que T-PERM-ALL no la interprete como permiso único.
        app.MapGet("/api/v1/dashboard",
                (GetDashboardHandler handler, IAuthorizationService authz, HttpContext httpContext, CancellationToken ct) =>
                    GetDashboardAsync(handler, authz, httpContext, ct))
            .RequireAuthorization(policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new HasPermissionRequirement(Permission.WorkOrdersRead));
                policy.AddRequirements(new HasPermissionRequirement(Permission.PaymentsRead));
            });

        return app;
    }

    private static async Task<IResult> GetDashboardAsync(
        GetDashboardHandler handler,
        IAuthorizationService authz,
        HttpContext httpContext,
        CancellationToken ct)
    {
        // Las secciones que dependen de otro permiso viajan solo si el rol lo tiene (no "vacías").
        async Task<bool> CanAsync(string permission) =>
            (await authz.AuthorizeAsync(httpContext.User, null, PermissionRequirement.PermissionPolicy(permission))).Succeeded;

        var scope = new DashboardScope(
            Quotes: await CanAsync(Permission.QuotesRead),
            Appointments: await CanAsync(Permission.AppointmentsRead),
            Cash: await CanAsync(Permission.CashRead),
            Stock: await CanAsync(Permission.CatalogRead));

        var dto = await handler.HandleAsync(scope, ct);
        return Results.Ok(dto);
    }
}
