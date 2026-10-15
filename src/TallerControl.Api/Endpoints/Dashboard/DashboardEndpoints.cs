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
                (GetDashboardHandler handler, CancellationToken ct) => GetDashboardAsync(handler, ct))
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
        CancellationToken ct)
    {
        var dto = await handler.HandleAsync(ct);
        return Results.Ok(dto);
    }
}
